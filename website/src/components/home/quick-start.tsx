export function QuickStart() {
  return (
    <section className="py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            Up and Running in Minutes
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            Clone the repo, start with Docker, and you&apos;re ready to go.
          </p>
        </div>
        <div className="mx-auto mt-12 max-w-2xl">
          <div className="overflow-hidden rounded-lg border bg-card">
            <div className="flex items-center gap-2 border-b bg-muted/50 px-4 py-3">
              <div className="h-3 w-3 rounded-full bg-red-500" />
              <div className="h-3 w-3 rounded-full bg-yellow-500" />
              <div className="h-3 w-3 rounded-full bg-green-500" />
              <span className="ml-2 text-xs text-muted-foreground">
                Terminal
              </span>
            </div>
            <pre className="overflow-x-auto p-6 text-sm">
              <code>
                <span className="text-muted-foreground">
                  # Clone the repository
                </span>
                {"\n"}
                <span className="text-primary">$</span> git clone
                https://github.com/Battle-Line-Productions/Courier.git{"\n"}
                <span className="text-primary">$</span> cd Courier{"\n"}
                {"\n"}
                <span className="text-muted-foreground">
                  # Install frontend dependencies
                </span>
                {"\n"}
                <span className="text-primary">$</span> cd
                src/Courier.Frontend &amp;&amp; npm install{"\n"}
                {"\n"}
                <span className="text-muted-foreground">
                  # Start everything with Aspire (API, Worker, Frontend,
                  Postgres, Seq)
                </span>
                {"\n"}
                <span className="text-primary">$</span> cd
                ../Courier.AppHost &amp;&amp; dotnet run{"\n"}
                {"\n"}
                <span className="text-green-600 dark:text-green-400">
                  &#10003; Dashboard ready at http://localhost:5000
                </span>
              </code>
            </pre>
          </div>
          <p className="mt-4 text-center text-sm text-muted-foreground">
            Prerequisites: .NET 10 SDK, Docker Desktop, Node.js 20+
          </p>
        </div>
      </div>
    </section>
  );
}
