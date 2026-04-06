import Link from "next/link";

const footerLinks = {
  Product: [
    { label: "Documentation", href: "/docs" },
    { label: "Screenshots", href: "/screenshots" },
    { label: "Getting Started", href: "/docs/getting-started" },
  ],
  Community: [
    {
      label: "GitHub",
      href: "https://github.com/Battle-Line-Productions/Courier",
      external: true,
    },
    {
      label: "Contributing",
      href: "/docs/contributing",
    },
    {
      label: "Issues",
      href: "https://github.com/Battle-Line-Productions/Courier/issues",
      external: true,
    },
  ],
  Legal: [
    {
      label: "License (Apache 2.0)",
      href: "https://github.com/Battle-Line-Productions/Courier/blob/main/LICENSE",
      external: true,
    },
    { label: "Security", href: "/docs/security" },
  ],
};

export function Footer() {
  return (
    <footer className="border-t bg-muted/40">
      <div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
        <div className="grid grid-cols-2 gap-8 md:grid-cols-3">
          {Object.entries(footerLinks).map(([category, links]) => (
            <div key={category}>
              <h3 className="text-sm font-semibold">{category}</h3>
              <ul className="mt-3 space-y-2">
                {links.map((link) => (
                  <li key={link.href}>
                    {"external" in link && link.external ? (
                      <a
                        href={link.href}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                      >
                        {link.label}
                      </a>
                    ) : (
                      <Link
                        href={link.href}
                        className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                      >
                        {link.label}
                      </Link>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <div className="mt-8 border-t pt-8 text-center text-sm text-muted-foreground">
          <p>
            &copy; {new Date().getFullYear()} Battle Line Productions. Released
            under the Apache 2.0 License.
          </p>
        </div>
      </div>
    </footer>
  );
}
