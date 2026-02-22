var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with persistent volume
var postgres = builder.AddPostgres("courier-db")
    .WithDataVolume("courier-pgdata")
    .AddDatabase("CourierDb");

// Seq for local structured logging
var seq = builder.AddSeq("seq")
    .WithLifetime(ContainerLifetime.Persistent);

// API Host
var api = builder.AddProject<Projects.Courier_Api>("courier-api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithReference(seq)
    .WithEnvironment("Serilog__WriteTo__0__Args__serverUrl", seq.GetEndpoint("http"));

// Worker Host
builder.AddProject<Projects.Courier_Worker>("courier-worker")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitFor(api)
    .WithReference(seq)
    .WithEnvironment("Serilog__WriteTo__0__Args__serverUrl", seq.GetEndpoint("http"));

// Frontend (npm dev server)
builder.AddJavaScriptApp("courier-frontend", "../Courier.Frontend", "dev")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithEnvironment("NEXT_PUBLIC_API_URL", api.GetEndpoint("http"));

builder.Build().Run();
