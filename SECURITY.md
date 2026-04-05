# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| Latest on `main` | Yes |

## Reporting a Vulnerability

**Please do NOT report security vulnerabilities through public GitHub issues.**

Use GitHub's private vulnerability reporting instead:

**[Report a vulnerability](https://github.com/Battle-Line-Productions/Courier/security/advisories/new)**

### What to Include

- Description of the vulnerability
- Steps to reproduce or proof of concept
- Affected component (API, Worker, Frontend, Encryption, Auth, etc.)
- Potential impact assessment
- Suggested fix (if you have one)

### What to Expect

- **Acknowledgment** within 48 hours
- **Status update** within 7 days with an assessment and timeline
- **Fix timeline** depends on severity:
  - Critical: patch within 7 days
  - High: patch within 30 days
  - Medium/Low: addressed in the next regular release

We will coordinate disclosure with you. We ask that you give us reasonable time to address the issue before public disclosure.

## Scope

### In scope

- Authentication and authorization bypasses
- Credential exposure or encryption weaknesses
- SQL injection, command injection, or path traversal
- Cross-site scripting (XSS) in the frontend
- Insecure defaults that could lead to data exposure
- Vulnerabilities in the job engine that could allow arbitrary code execution

### Out of scope

- Denial of service (DoS) attacks
- Social engineering
- Issues in third-party dependencies (report those upstream, but let us know)
- Issues requiring physical access to the server

## Security Design

Courier is designed with security in mind:

- **Encryption at rest**: AES-256-GCM envelope encryption for all stored credentials and keys
- **Authentication**: JWT with refresh token rotation; OIDC and SAML SSO support
- **Authorization**: Role-based access control (Admin, Operator, Viewer) with 23+ granular permissions
- **Audit trail**: All entity operations logged with performer, timestamp, and change details
- **Credential handling**: Encrypted in database, never logged, never returned in API responses
- **Host key verification**: Trust-on-first-use, pinned, or manual policies for SSH/TLS
- **FIPS**: Optional FIPS enforcement mode for regulated environments

## Acknowledgments

We appreciate the security research community. Contributors who report valid vulnerabilities will be credited in the release notes (unless they prefer to remain anonymous).
