const technologies = [
  { name: ".NET 10", category: "Backend" },
  { name: "ASP.NET Core", category: "API" },
  { name: "PostgreSQL 16", category: "Database" },
  { name: "Next.js", category: "Frontend" },
  { name: "React 19", category: "UI" },
  { name: "Docker", category: "Deployment" },
  { name: "Quartz.NET", category: "Scheduling" },
  { name: "BouncyCastle", category: "Cryptography" },
];

export function TechStack() {
  return (
    <section className="py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            Built on Proven Technology
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            Enterprise-grade stack you already know and trust.
          </p>
        </div>
        <div className="mx-auto mt-12 flex max-w-3xl flex-wrap items-center justify-center gap-4">
          {technologies.map((tech) => (
            <div
              key={tech.name}
              className="flex items-center gap-2 rounded-full border bg-card px-4 py-2"
            >
              <span className="text-sm font-medium">{tech.name}</span>
              <span className="text-xs text-muted-foreground">
                {tech.category}
              </span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
