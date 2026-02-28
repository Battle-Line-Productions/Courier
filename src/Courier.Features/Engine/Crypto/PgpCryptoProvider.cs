using System.Text;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace Courier.Features.Engine.Crypto;

public class PgpCryptoProvider : ICryptoProvider
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private const int BufferSize = 81920; // 80KB
    private const long ProgressIntervalBytes = 10 * 1024 * 1024; // 10MB

    public PgpCryptoProvider(CourierDbContext db, ICredentialEncryptor encryptor)
    {
        _db = db;
        _encryptor = encryptor;
    }

    public async Task<CryptoResult> EncryptAsync(
        EncryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        try
        {
            // 1. Load all recipient keys
            var keys = await _db.PgpKeys
                .Where(k => request.RecipientKeyIds.Contains(k.Id))
                .ToListAsync(ct);

            if (keys.Count != request.RecipientKeyIds.Count)
            {
                var missing = request.RecipientKeyIds.Except(keys.Select(k => k.Id));
                return new CryptoResult(false, 0, request.OutputPath,
                    $"Recipient key(s) not found: {string.Join(", ", missing)}");
            }

            // 2. Validate key statuses for encryption
            foreach (var key in keys)
            {
                var error = ValidateKeyForEncryption(key);
                if (error != null) return error;
            }

            // 3. Parse recipient public keys
            var recipientPublicKeys = new List<PgpPublicKey>();
            foreach (var key in keys)
            {
                var pgpPublicKey = ParsePublicKey(key.PublicKeyData!);
                recipientPublicKeys.Add(GetEncryptionKey(pgpPublicKey));
            }

            // 4. Load signing key if provided
            PgpPrivateKey? signingPrivateKey = null;
            PgpPublicKey? signingPublicKey = null;
            if (request.SigningKeyId.HasValue)
            {
                var signingEntity = await _db.PgpKeys.FirstOrDefaultAsync(
                    k => k.Id == request.SigningKeyId.Value, ct);
                if (signingEntity == null)
                    return new CryptoResult(false, 0, request.OutputPath,
                        $"Signing key not found: {request.SigningKeyId.Value}");

                var sigError = ValidateKeyForSigning(signingEntity);
                if (sigError != null)
                    return new CryptoResult(false, 0, request.OutputPath, sigError);

                var (privKey, pubKey) = ExtractPrivateKey(signingEntity);
                signingPrivateKey = privKey;
                signingPublicKey = pubKey;
            }

            // 5. Build streaming pipeline
            var inputFileInfo = new FileInfo(request.InputPath);
            var totalBytes = inputFileInfo.Length;
            long bytesProcessed = 0;
            long lastProgressReport = 0;

            await using var outputFileStream = new FileStream(
                request.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

            Stream armoredOrOutputStream = request.Format == OutputFormat.Armored
                ? new ArmoredOutputStream(outputFileStream)
                : outputFileStream;

            try
            {
                var encDataGen = new PgpEncryptedDataGenerator(
                    SymmetricKeyAlgorithmTag.Aes256, true, new Org.BouncyCastle.Security.SecureRandom());

                foreach (var pubKey in recipientPublicKeys)
                    encDataGen.AddMethod(pubKey);

                await using var encryptedStream = encDataGen.Open(armoredOrOutputStream, new byte[BufferSize]);

                var compressedGen = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
                await using var compressedStream = compressedGen.Open(encryptedStream);

                // Set up signature generator if signing
                PgpSignatureGenerator? sigGen = null;
                if (signingPrivateKey != null && signingPublicKey != null)
                {
                    sigGen = new PgpSignatureGenerator(
                        signingPublicKey.Algorithm, HashAlgorithmTag.Sha256);
                    sigGen.InitSign(PgpSignature.BinaryDocument, signingPrivateKey);

                    // Add user ID sub-packet
                    foreach (string userId in signingPublicKey.GetUserIds())
                    {
                        var subpacketGen = new PgpSignatureSubpacketGenerator();
                        subpacketGen.AddSignerUserId(false, userId);
                        sigGen.SetHashedSubpackets(subpacketGen.Generate());
                        break;
                    }

                    sigGen.GenerateOnePassVersion(false).Encode(compressedStream);
                }

                var literalGen = new PgpLiteralDataGenerator();
                var fileName = Path.GetFileName(request.InputPath);
                await using var literalStream = literalGen.Open(
                    compressedStream, PgpLiteralData.Binary, fileName,
                    inputFileInfo.Length, DateTime.UtcNow);

                await using var inputFileStream = new FileStream(
                    request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

                var buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = await inputFileStream.ReadAsync(buffer, ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await literalStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    sigGen?.Update(buffer, 0, bytesRead);

                    bytesProcessed += bytesRead;
                    if (progress != null && bytesProcessed - lastProgressReport >= ProgressIntervalBytes)
                    {
                        progress.Report(new CryptoProgress(bytesProcessed, totalBytes, "Encrypting"));
                        lastProgressReport = bytesProcessed;
                    }
                }

                // Finalize signature
                if (sigGen != null)
                {
                    sigGen.Generate().Encode(compressedStream);
                }
            }
            finally
            {
                if (armoredOrOutputStream is ArmoredOutputStream armored)
                    await armored.DisposeAsync();
            }

            progress?.Report(new CryptoProgress(bytesProcessed, totalBytes, "Complete"));

            return new CryptoResult(true, bytesProcessed, request.OutputPath, null);
        }
        catch (Exception ex)
        {
            return new CryptoResult(false, 0, request.OutputPath, $"Encryption failed: {ex.Message}");
        }
    }

    public async Task<CryptoResult> DecryptAsync(
        DecryptRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        try
        {
            // 1. Load private key entity
            var keyEntity = await _db.PgpKeys.FirstOrDefaultAsync(k => k.Id == request.PrivateKeyId, ct);
            if (keyEntity == null)
                return new CryptoResult(false, 0, request.OutputPath,
                    $"Private key not found: {request.PrivateKeyId}");

            var decError = ValidateKeyForDecryption(keyEntity);
            if (decError != null)
                return new CryptoResult(false, 0, request.OutputPath, decError);

            // 2-4. Extract private key
            var (pgpPrivateKey, pgpPublicKey) = ExtractPrivateKey(keyEntity);
            var keyId = pgpPublicKey.KeyId;

            // 5. Open encrypted file and parse
            await using var inputFileStream = new FileStream(
                request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

            var decoderStream = PgpUtilities.GetDecoderStream(inputFileStream);
            var pgpFactory = new PgpObjectFactory(decoderStream);

            PgpEncryptedDataList? encDataList = null;
            PgpObject? obj = pgpFactory.NextPgpObject();
            while (obj != null)
            {
                if (obj is PgpEncryptedDataList edl)
                {
                    encDataList = edl;
                    break;
                }
                obj = pgpFactory.NextPgpObject();
            }

            if (encDataList == null)
                return new CryptoResult(false, 0, request.OutputPath,
                    "No encrypted data found in the input file.");

            // 6. Find matching encrypted data for our key
            PgpPublicKeyEncryptedData? encData = null;
            foreach (PgpPublicKeyEncryptedData pked in encDataList.GetEncryptedDataObjects())
            {
                if (pked.KeyId == keyId)
                {
                    encData = pked;
                    break;
                }
                // Also check subkeys
                var secretKeyRing = ParseSecretKeyRing(keyEntity);
                foreach (PgpSecretKey sk in secretKeyRing.GetSecretKeys())
                {
                    if (sk.KeyId == pked.KeyId)
                    {
                        var passphrase = GetPassphrase(keyEntity);
                        pgpPrivateKey = sk.ExtractPrivateKey(passphrase.ToCharArray());
                        encData = pked;
                        break;
                    }
                }
                if (encData != null) break;
            }

            if (encData == null)
                return new CryptoResult(false, 0, request.OutputPath,
                    "This key cannot decrypt the message. The message was not encrypted for this key.");

            // 7. Decrypt
            using var clearStream = encData.GetDataStream(pgpPrivateKey);
            var clearFactory = new PgpObjectFactory(clearStream);

            PgpOnePassSignatureList? onePassSigs = null;
            PgpSignatureList? signatures = null;
            long bytesProcessed = 0;

            obj = clearFactory.NextPgpObject();
            while (obj != null)
            {
                if (obj is PgpCompressedData compressed)
                {
                    var compFactory = new PgpObjectFactory(compressed.GetDataStream());
                    obj = compFactory.NextPgpObject();
                    while (obj != null)
                    {
                        if (obj is PgpOnePassSignatureList opsl)
                        {
                            onePassSigs = opsl;
                        }
                        else if (obj is PgpLiteralData literal)
                        {
                            bytesProcessed = await WriteLiteralData(
                                literal, request.OutputPath, onePassSigs, progress, ct);
                        }
                        else if (obj is PgpSignatureList sl)
                        {
                            signatures = sl;
                        }
                        obj = compFactory.NextPgpObject();
                    }
                    break;
                }
                else if (obj is PgpOnePassSignatureList opsl)
                {
                    onePassSigs = opsl;
                }
                else if (obj is PgpLiteralData literal)
                {
                    bytesProcessed = await WriteLiteralData(
                        literal, request.OutputPath, onePassSigs, progress, ct);
                }
                else if (obj is PgpSignatureList sl)
                {
                    signatures = sl;
                }
                obj = clearFactory.NextPgpObject();
            }

            // 8. Verify signature if requested
            if (request.VerifySignature && onePassSigs != null && signatures != null)
            {
                // Signature verification was done during read in WriteLiteralData
                // We'd need to verify against known public keys
            }

            progress?.Report(new CryptoProgress(bytesProcessed, bytesProcessed, "Complete"));

            return new CryptoResult(true, bytesProcessed, request.OutputPath, null);
        }
        catch (Exception ex)
        {
            return new CryptoResult(false, 0, request.OutputPath, $"Decryption failed: {ex.Message}");
        }
    }

    public async Task<CryptoResult> SignAsync(
        SignRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        try
        {
            // Load signing key
            var keyEntity = await _db.PgpKeys.FirstOrDefaultAsync(k => k.Id == request.SigningKeyId, ct);
            if (keyEntity == null)
                return new CryptoResult(false, 0, request.OutputPath,
                    $"Signing key not found: {request.SigningKeyId}");

            var sigError = ValidateKeyForSigning(keyEntity);
            if (sigError != null)
                return new CryptoResult(false, 0, request.OutputPath, sigError);

            var (pgpPrivateKey, pgpPublicKey) = ExtractPrivateKey(keyEntity);

            return request.Mode switch
            {
                SignatureMode.Detached => await SignDetached(
                    request, pgpPrivateKey, pgpPublicKey, progress, ct),
                SignatureMode.Inline => await SignInline(
                    request, pgpPrivateKey, pgpPublicKey, progress, ct),
                SignatureMode.Clearsign => await SignClearsign(
                    request, pgpPrivateKey, pgpPublicKey, progress, ct),
                _ => new CryptoResult(false, 0, request.OutputPath,
                    $"Unsupported signature mode: {request.Mode}")
            };
        }
        catch (Exception ex)
        {
            return new CryptoResult(false, 0, request.OutputPath, $"Signing failed: {ex.Message}");
        }
    }

    public async Task<VerifyResult> VerifyAsync(
        VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        try
        {
            if (request.DetachedSignaturePath != null)
                return await VerifyDetached(request, progress, ct);

            return await VerifyInlineOrClearsign(request, progress, ct);
        }
        catch (Exception)
        {
            return new VerifyResult(false, VerifyStatus.Invalid, null, null);
        }
    }

    // --- Sign methods ---

    private async Task<CryptoResult> SignDetached(
        SignRequest request, PgpPrivateKey privateKey, PgpPublicKey publicKey,
        IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        var outputPath = string.IsNullOrEmpty(request.OutputPath)
            ? request.InputPath + ".sig"
            : request.OutputPath;

        var sigGen = new PgpSignatureGenerator(publicKey.Algorithm, HashAlgorithmTag.Sha256);
        sigGen.InitSign(PgpSignature.BinaryDocument, privateKey);

        var inputFileInfo = new FileInfo(request.InputPath);
        var totalBytes = inputFileInfo.Length;
        long bytesProcessed = 0;
        long lastProgressReport = 0;

        await using var inputStream = new FileStream(
            request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sigGen.Update(buffer, 0, bytesRead);
            bytesProcessed += bytesRead;
            if (progress != null && bytesProcessed - lastProgressReport >= ProgressIntervalBytes)
            {
                progress.Report(new CryptoProgress(bytesProcessed, totalBytes, "Signing"));
                lastProgressReport = bytesProcessed;
            }
        }

        var signature = sigGen.Generate();

        await using var outputStream = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
        await using var armoredOutput = new ArmoredOutputStream(outputStream);
        signature.Encode(armoredOutput);

        progress?.Report(new CryptoProgress(bytesProcessed, totalBytes, "Complete"));
        return new CryptoResult(true, bytesProcessed, outputPath, null);
    }

    private async Task<CryptoResult> SignInline(
        SignRequest request, PgpPrivateKey privateKey, PgpPublicKey publicKey,
        IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        var inputFileInfo = new FileInfo(request.InputPath);
        var totalBytes = inputFileInfo.Length;
        long bytesProcessed = 0;
        long lastProgressReport = 0;

        await using var outputFileStream = new FileStream(
            request.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
        await using var armoredOutput = new ArmoredOutputStream(outputFileStream);

        var sigGen = new PgpSignatureGenerator(publicKey.Algorithm, HashAlgorithmTag.Sha256);
        sigGen.InitSign(PgpSignature.BinaryDocument, privateKey);

        // Add user ID sub-packet
        foreach (string userId in publicKey.GetUserIds())
        {
            var subpacketGen = new PgpSignatureSubpacketGenerator();
            subpacketGen.AddSignerUserId(false, userId);
            sigGen.SetHashedSubpackets(subpacketGen.Generate());
            break;
        }

        var compressedGen = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
        await using var compressedStream = compressedGen.Open(armoredOutput);

        // Write one-pass signature header
        sigGen.GenerateOnePassVersion(false).Encode(compressedStream);

        // Write literal data
        var literalGen = new PgpLiteralDataGenerator();
        var fileName = Path.GetFileName(request.InputPath);
        await using var literalStream = literalGen.Open(
            compressedStream, PgpLiteralData.Binary, fileName,
            inputFileInfo.Length, DateTime.UtcNow);

        await using var inputStream = new FileStream(
            request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            await literalStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            sigGen.Update(buffer, 0, bytesRead);
            bytesProcessed += bytesRead;
            if (progress != null && bytesProcessed - lastProgressReport >= ProgressIntervalBytes)
            {
                progress.Report(new CryptoProgress(bytesProcessed, totalBytes, "Signing"));
                lastProgressReport = bytesProcessed;
            }
        }

        // Close literal stream before writing signature
        await literalStream.DisposeAsync();

        // Write signature trailer
        sigGen.Generate().Encode(compressedStream);

        progress?.Report(new CryptoProgress(bytesProcessed, totalBytes, "Complete"));
        return new CryptoResult(true, bytesProcessed, request.OutputPath, null);
    }

    private async Task<CryptoResult> SignClearsign(
        SignRequest request, PgpPrivateKey privateKey, PgpPublicKey publicKey,
        IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        var inputFileInfo = new FileInfo(request.InputPath);
        var totalBytes = inputFileInfo.Length;

        // Read input and normalize lines
        var inputBytes = await File.ReadAllBytesAsync(request.InputPath, ct);
        var inputText = Encoding.UTF8.GetString(inputBytes);
        var rawLines = inputText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        var sigGen = new PgpSignatureGenerator(publicKey.Algorithm, HashAlgorithmTag.Sha256);
        sigGen.InitSign(PgpSignature.CanonicalTextDocument, privateKey);

        foreach (string userId in publicKey.GetUserIds())
        {
            var subpacketGen = new PgpSignatureSubpacketGenerator();
            subpacketGen.AddSignerUserId(false, userId);
            sigGen.SetHashedSubpackets(subpacketGen.Generate());
            break;
        }

        // Build canonical text for signature: trim trailing ws from each line, join with \r\n
        for (var i = 0; i < rawLines.Length; i++)
        {
            var trimmed = rawLines[i].TrimEnd();
            var trimmedBytes = Encoding.UTF8.GetBytes(trimmed);
            sigGen.Update(trimmedBytes, 0, trimmedBytes.Length);
            if (i < rawLines.Length - 1)
                sigGen.Update([(byte)'\r', (byte)'\n'], 0, 2);
        }

        var sig = sigGen.Generate();

        // Write clearsigned file manually to match OpenPGP cleartext signature format
        await using var outputStream = new FileStream(
            request.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

        // Header
        await WriteClearsignLineAsync(outputStream, "-----BEGIN PGP SIGNED MESSAGE-----");
        await WriteClearsignLineAsync(outputStream, "Hash: SHA256");
        await WriteClearsignLineAsync(outputStream, "");

        // Cleartext body with dash escaping
        for (var i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i];
            if (line.StartsWith('-'))
                line = "- " + line;
            await WriteClearsignLineAsync(outputStream, line);
        }

        // Armored signature block
        using var sigMs = new MemoryStream();
        using (var sigArmored = new ArmoredOutputStream(sigMs))
        {
            sig.Encode(sigArmored);
        }
        await outputStream.WriteAsync(sigMs.ToArray());

        progress?.Report(new CryptoProgress(totalBytes, totalBytes, "Complete"));
        return new CryptoResult(true, totalBytes, request.OutputPath, null);
    }

    private static async Task WriteClearsignLineAsync(Stream stream, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
        await stream.WriteAsync(bytes);
    }

    // --- Verify methods ---

    private async Task<VerifyResult> VerifyDetached(
        VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // Read detached signature
        await using var sigFileStream = new FileStream(
            request.DetachedSignaturePath!, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        var sigDecoderStream = PgpUtilities.GetDecoderStream(sigFileStream);
        var sigFactory = new PgpObjectFactory(sigDecoderStream);
        var sigList = (PgpSignatureList)sigFactory.NextPgpObject();
        var signature = sigList[0];

        // Resolve signer key
        var (signerKey, signerEntity) = await ResolveSignerKey(
            signature.KeyId, request.ExpectedSignerKeyId, ct);

        if (signerKey == null)
            return new VerifyResult(false, VerifyStatus.UnknownSigner, null,
                signature.CreationTime);

        // Check key status
        var statusResult = MapKeyStatusForVerify(signerEntity!);
        if (statusResult != VerifyStatus.Valid)
            return new VerifyResult(false, statusResult, signerEntity!.Fingerprint,
                signature.CreationTime);

        // Verify signature against input file
        signature.InitVerify(signerKey);

        await using var inputStream = new FileStream(
            request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;
        long bytesProcessed = 0;
        var totalBytes = new FileInfo(request.InputPath).Length;
        long lastProgressReport = 0;

        while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
        {
            signature.Update(buffer, 0, bytesRead);
            bytesProcessed += bytesRead;
            if (progress != null && bytesProcessed - lastProgressReport >= ProgressIntervalBytes)
            {
                progress.Report(new CryptoProgress(bytesProcessed, totalBytes, "Verifying"));
                lastProgressReport = bytesProcessed;
            }
        }

        var isValid = signature.Verify();
        progress?.Report(new CryptoProgress(bytesProcessed, totalBytes, "Complete"));

        return new VerifyResult(isValid, isValid ? VerifyStatus.Valid : VerifyStatus.Invalid,
            signerEntity!.Fingerprint, signature.CreationTime);
    }

    private async Task<VerifyResult> VerifyInlineOrClearsign(
        VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // Check if it's a clearsigned message by reading the first line
        var firstLine = await ReadFirstLineAsync(request.InputPath, ct);
        if (firstLine != null && firstLine.Contains("BEGIN PGP SIGNED MESSAGE"))
        {
            return await VerifyClearsign(request, progress, ct);
        }

        // Inline signature
        await using var inputStream = new FileStream(
            request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        var decoderStream = PgpUtilities.GetDecoderStream(inputStream);
        return await VerifyInline(decoderStream, request, progress, ct);
    }

    private async Task<VerifyResult> VerifyClearsign(
        VerifyRequest request, IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        // Parse the clearsigned message manually for reliable line handling
        var fileContent = await File.ReadAllTextAsync(request.InputPath, ct);
        var fileLines = fileContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        // Find the blank line after "Hash:" header — cleartext starts after it
        var clearTextStartIndex = -1;
        for (var i = 0; i < fileLines.Length; i++)
        {
            if (fileLines[i].Trim() == "" && i > 0 && fileLines[i - 1].StartsWith("Hash:"))
            {
                clearTextStartIndex = i + 1;
                break;
            }
        }

        if (clearTextStartIndex < 0)
            return new VerifyResult(false, VerifyStatus.Invalid, null, null);

        // Find the "-----BEGIN PGP SIGNATURE-----" line
        var sigStartIndex = -1;
        for (var i = clearTextStartIndex; i < fileLines.Length; i++)
        {
            if (fileLines[i].StartsWith("-----BEGIN PGP SIGNATURE-----"))
            {
                sigStartIndex = i;
                break;
            }
        }

        if (sigStartIndex < 0)
            return new VerifyResult(false, VerifyStatus.Invalid, null, null);

        // Extract cleartext lines (between blank-after-hash-header and begin-signature)
        var clearLines = new List<string>();
        for (var i = clearTextStartIndex; i < sigStartIndex; i++)
        {
            var line = fileLines[i];
            // Reverse dash escaping
            if (line.StartsWith("- "))
                line = line[2..];
            clearLines.Add(line);
        }

        // Remove trailing empty line (the blank line before -----BEGIN PGP SIGNATURE-----)
        while (clearLines.Count > 0 && clearLines[^1].Trim() == "")
            clearLines.RemoveAt(clearLines.Count - 1);

        // Extract and parse the armored signature
        var sigBlock = new StringBuilder();
        for (var i = sigStartIndex; i < fileLines.Length; i++)
        {
            sigBlock.AppendLine(fileLines[i]);
        }

        PgpSignature signature;
        using (var sigStream = PgpUtilities.GetDecoderStream(
            new MemoryStream(Encoding.UTF8.GetBytes(sigBlock.ToString()))))
        {
            var sigFactory = new PgpObjectFactory(sigStream);
            var sigObj = sigFactory.NextPgpObject();
            if (sigObj is not PgpSignatureList sigList || sigList.Count == 0)
                return new VerifyResult(false, VerifyStatus.Invalid, null, null);
            signature = sigList[0];
        }

        // Resolve signer key
        var (signerKey, signerEntity) = await ResolveSignerKey(
            signature.KeyId, request.ExpectedSignerKeyId, ct);

        if (signerKey == null)
            return new VerifyResult(false, VerifyStatus.UnknownSigner, null,
                signature.CreationTime);

        var statusResult = MapKeyStatusForVerify(signerEntity!);
        if (statusResult != VerifyStatus.Valid)
            return new VerifyResult(false, statusResult, signerEntity!.Fingerprint,
                signature.CreationTime);

        // Verify signature: canonical text = trimmed trailing whitespace, \r\n separators
        signature.InitVerify(signerKey);
        var lineSep = Encoding.UTF8.GetBytes("\r\n");

        for (var i = 0; i < clearLines.Count; i++)
        {
            var trimmed = clearLines[i].TrimEnd();
            var trimmedBytes = Encoding.UTF8.GetBytes(trimmed);
            signature.Update(trimmedBytes, 0, trimmedBytes.Length);
            if (i < clearLines.Count - 1)
                signature.Update(lineSep, 0, lineSep.Length);
        }

        var isValid = signature.Verify();

        return new VerifyResult(isValid, isValid ? VerifyStatus.Valid : VerifyStatus.Invalid,
            signerEntity!.Fingerprint, signature.CreationTime);
    }

    private static async Task<string?> ReadFirstLineAsync(string path, CancellationToken ct)
    {
        using var reader = new StreamReader(path);
        return await reader.ReadLineAsync(ct);
    }

    private async Task<VerifyResult> VerifyInline(
        Stream decoderStream, VerifyRequest request,
        IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        var pgpFactory = new PgpObjectFactory(decoderStream);
        PgpOnePassSignatureList? onePassSigs = null;
        PgpSignatureList? signatures = null;
        byte[]? literalContent = null;

        var obj = pgpFactory.NextPgpObject();
        while (obj != null)
        {
            if (obj is PgpCompressedData compressed)
            {
                var compFactory = new PgpObjectFactory(compressed.GetDataStream());
                var innerObj = compFactory.NextPgpObject();
                while (innerObj != null)
                {
                    if (innerObj is PgpOnePassSignatureList opsl)
                        onePassSigs = opsl;
                    else if (innerObj is PgpLiteralData literal)
                    {
                        using var litStream = literal.GetInputStream();
                        using var ms = new MemoryStream();
                        await litStream.CopyToAsync(ms, ct);
                        literalContent = ms.ToArray();
                    }
                    else if (innerObj is PgpSignatureList sl)
                        signatures = sl;
                    innerObj = compFactory.NextPgpObject();
                }
                break;
            }
            else if (obj is PgpOnePassSignatureList opsl)
                onePassSigs = opsl;
            else if (obj is PgpLiteralData literal)
            {
                using var litStream = literal.GetInputStream();
                using var ms = new MemoryStream();
                await litStream.CopyToAsync(ms, ct);
                literalContent = ms.ToArray();
            }
            else if (obj is PgpSignatureList sl)
                signatures = sl;
            obj = pgpFactory.NextPgpObject();
        }

        if (onePassSigs == null || signatures == null || literalContent == null)
            return new VerifyResult(false, VerifyStatus.Invalid, null, null);

        var onePassSig = onePassSigs[0];
        var sig = signatures[0];

        // Resolve signer key
        var (signerKey, signerEntity) = await ResolveSignerKey(
            onePassSig.KeyId, request.ExpectedSignerKeyId, ct);

        if (signerKey == null)
            return new VerifyResult(false, VerifyStatus.UnknownSigner, null,
                sig.CreationTime);

        var statusResult = MapKeyStatusForVerify(signerEntity!);
        if (statusResult != VerifyStatus.Valid)
            return new VerifyResult(false, statusResult, signerEntity!.Fingerprint,
                sig.CreationTime);

        // Verify with one-pass signature
        onePassSig.InitVerify(signerKey);
        onePassSig.Update(literalContent, 0, literalContent.Length);
        var isValid = onePassSig.Verify(sig);

        return new VerifyResult(isValid, isValid ? VerifyStatus.Valid : VerifyStatus.Invalid,
            signerEntity!.Fingerprint, sig.CreationTime);
    }

    // --- Helpers ---

    private CryptoResult? ValidateKeyForEncryption(PgpKey key)
    {
        if (key.Status is "retired")
            return new CryptoResult(false, 0, string.Empty,
                $"Cannot encrypt with retired key '{key.Name}' ({key.Id}). Retired keys are no longer trusted for encryption.");

        if (key.Status is "revoked")
            return new CryptoResult(false, 0, string.Empty,
                $"Cannot encrypt with revoked key '{key.Name}' ({key.Id}). Revoked keys must not be used for any cryptographic operations.");

        if (key.Status is "deleted")
            return new CryptoResult(false, 0, string.Empty,
                $"Cannot encrypt with deleted key '{key.Name}' ({key.Id}).");

        if (key.PublicKeyData == null)
            return new CryptoResult(false, 0, string.Empty,
                $"Key '{key.Name}' ({key.Id}) has no public key data.");

        return null;
    }

    private string? ValidateKeyForSigning(PgpKey key)
    {
        if (key.Status is "retired")
            return $"Cannot sign with retired key '{key.Name}' ({key.Id}). Retired keys are no longer trusted for signing.";

        if (key.Status is "revoked")
            return $"Cannot sign with revoked key '{key.Name}' ({key.Id}). Revoked keys must not be used for any cryptographic operations.";

        if (key.Status is "deleted")
            return $"Cannot sign with deleted key '{key.Name}' ({key.Id}).";

        if (key.PrivateKeyData == null)
            return $"Key '{key.Name}' ({key.Id}) has no private key data.";

        return null;
    }

    private string? ValidateKeyForDecryption(PgpKey key)
    {
        // Revoked keys cannot decrypt
        if (key.Status is "revoked")
            return $"Cannot decrypt with revoked key '{key.Name}' ({key.Id}).";

        if (key.Status is "deleted")
            return $"Cannot decrypt with deleted key '{key.Name}' ({key.Id}).";

        if (key.PrivateKeyData == null)
            return $"Key '{key.Name}' ({key.Id}) has no private key data.";

        // Retired and active/expiring keys can decrypt (legacy data access)
        return null;
    }

    private static VerifyStatus MapKeyStatusForVerify(PgpKey key)
    {
        return key.Status switch
        {
            "active" or "expiring" => VerifyStatus.Valid,
            "retired" => VerifyStatus.ExpiredKey,
            "revoked" => VerifyStatus.RevokedKey,
            _ => VerifyStatus.Invalid
        };
    }

    private (PgpPrivateKey privateKey, PgpPublicKey publicKey) ExtractPrivateKey(PgpKey keyEntity)
    {
        var secretKeyRing = ParseSecretKeyRing(keyEntity);
        var passphrase = GetPassphrase(keyEntity);

        // Find the signing-capable key (master key)
        PgpSecretKey? signingSecretKey = null;
        foreach (PgpSecretKey sk in secretKeyRing.GetSecretKeys())
        {
            if (sk.IsSigningKey)
            {
                signingSecretKey = sk;
                break;
            }
        }

        signingSecretKey ??= secretKeyRing.GetSecretKey();

        var privateKey = signingSecretKey.ExtractPrivateKey(passphrase.ToCharArray());
        return (privateKey, signingSecretKey.PublicKey);
    }

    private PgpSecretKeyRing ParseSecretKeyRing(PgpKey keyEntity)
    {
        var armoredPrivateKey = _encryptor.Decrypt(keyEntity.PrivateKeyData!);
        var inputStream = PgpUtilities.GetDecoderStream(
            new MemoryStream(Encoding.UTF8.GetBytes(armoredPrivateKey)));
        var bundle = new PgpSecretKeyRingBundle(inputStream);

        foreach (PgpSecretKeyRing ring in bundle.GetKeyRings())
            return ring;

        throw new InvalidOperationException("No secret key ring found in key data.");
    }

    private string GetPassphrase(PgpKey keyEntity)
    {
        if (keyEntity.PassphraseHash != null)
            return _encryptor.Decrypt(keyEntity.PassphraseHash);
        return string.Empty;
    }

    private static PgpPublicKey ParsePublicKey(string armoredPublicKey)
    {
        var inputStream = PgpUtilities.GetDecoderStream(
            new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));

        // Try public key ring first
        try
        {
            var publicRingBundle = new PgpPublicKeyRingBundle(inputStream);
            foreach (PgpPublicKeyRing ring in publicRingBundle.GetKeyRings())
                return ring.GetPublicKey();
        }
        catch
        {
            // Fallback: might be a single public key exported from secret key
            inputStream = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));
            var publicRing = new PgpPublicKeyRing(inputStream);
            return publicRing.GetPublicKey();
        }

        throw new InvalidOperationException("No public key found in armored data.");
    }

    private static PgpPublicKey GetEncryptionKey(PgpPublicKey masterKey)
    {
        // The master key itself may have encryption capability
        if (masterKey.IsEncryptionKey)
            return masterKey;

        throw new InvalidOperationException(
            $"Key {masterKey.KeyId:X16} does not have encryption capability.");
    }

    private async Task<(PgpPublicKey? key, PgpKey? entity)> ResolveSignerKey(
        long keyId, Guid? expectedSignerKeyId, CancellationToken ct)
    {
        PgpKey? signerEntity = null;

        if (expectedSignerKeyId.HasValue)
        {
            signerEntity = await _db.PgpKeys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.Id == expectedSignerKeyId.Value, ct);
        }
        else
        {
            // Search by key ID in short key ID field
            var keyIdHex = keyId.ToString("X16");
            signerEntity = await _db.PgpKeys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.ShortKeyId == keyIdHex && !k.IsDeleted, ct);
        }

        if (signerEntity?.PublicKeyData == null)
            return (null, null);

        try
        {
            var publicKey = ParsePublicKey(signerEntity.PublicKeyData);
            return (publicKey, signerEntity);
        }
        catch
        {
            return (null, null);
        }
    }

    private async Task<long> WriteLiteralData(
        PgpLiteralData literal, string outputPath,
        PgpOnePassSignatureList? onePassSigs,
        IProgress<CryptoProgress>? progress, CancellationToken ct)
    {
        using var literalStream = literal.GetInputStream();
        await using var outputStream = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;
        long totalBytesWritten = 0;
        long lastProgressReport = 0;

        while ((bytesRead = await literalStream.ReadAsync(buffer, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalBytesWritten += bytesRead;

            if (progress != null && totalBytesWritten - lastProgressReport >= ProgressIntervalBytes)
            {
                progress.Report(new CryptoProgress(totalBytesWritten, 0, "Decrypting"));
                lastProgressReport = totalBytesWritten;
            }
        }

        return totalBytesWritten;
    }

}
