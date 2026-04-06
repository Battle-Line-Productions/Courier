import {
  ArrowRightLeft,
  FileKey,
  CalendarClock,
  ShieldCheck,
  Activity,
  Link as LinkIcon,
  FolderSearch,
  Zap,
} from "lucide-react";

const features = [
  {
    icon: ArrowRightLeft,
    title: "Multi-Step Job Pipelines",
    description:
      "Chain file operations, transfers, encryption, and custom steps into automated workflows.",
  },
  {
    icon: LinkIcon,
    title: "SFTP, FTP & Local Connections",
    description:
      "Manage connections to remote servers with credential encryption and connection pooling.",
  },
  {
    icon: FileKey,
    title: "PGP & AES-256 Encryption",
    description:
      "Encrypt and decrypt files with PGP keys or AES-256-GCM. Full key lifecycle management.",
  },
  {
    icon: CalendarClock,
    title: "Scheduling & Triggers",
    description:
      "Run jobs on cron schedules or trigger them via API. Quartz.NET persistent scheduler.",
  },
  {
    icon: ShieldCheck,
    title: "RBAC & Audit Trail",
    description:
      "Role-based access control with Admin, Operator, and Viewer roles. Every action logged.",
  },
  {
    icon: Activity,
    title: "Monitoring & Alerts",
    description:
      "Watch directories for new files. Get notified on job failures via configurable notifications.",
  },
  {
    icon: FolderSearch,
    title: "File Operations",
    description:
      "Copy, move, compress, and transform files as pipeline steps with context passing between steps.",
  },
  {
    icon: Zap,
    title: "Azure Function Integration",
    description:
      "Trigger Azure Functions as pipeline steps with callback support for async workflows.",
  },
];

export function Features() {
  return (
    <section className="border-t bg-muted/30 py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            Everything You Need for Managed File Transfer
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            A complete platform for automating, securing, and monitoring file
            transfer operations.
          </p>
        </div>
        <div className="mt-16 grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((feature) => (
            <div
              key={feature.title}
              className="group rounded-lg border bg-card p-6 transition-colors hover:border-primary/50"
            >
              <feature.icon className="h-8 w-8 text-primary" />
              <h3 className="mt-4 font-semibold">{feature.title}</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                {feature.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
