export interface Screenshot {
  src: string;
  alt: string;
  caption: string;
  category: ScreenshotCategory;
}

export type ScreenshotCategory =
  | "Dashboard"
  | "Jobs"
  | "Connections"
  | "Keys"
  | "Monitoring"
  | "Admin"
  | "Chains"
  | "Notifications";

export const screenshots: Screenshot[] = [
  {
    src: "/screenshots/dashboard.png",
    alt: "Courier MFT main dashboard with job execution overview and system health",
    caption: "Dashboard Overview",
    category: "Dashboard",
  },
  {
    src: "/screenshots/login-page.png",
    alt: "Courier MFT login page with Entra ID authentication",
    caption: "Login Page",
    category: "Dashboard",
  },
  {
    src: "/screenshots/sidebar-collapsed.png",
    alt: "Collapsed sidebar navigation showing icon-only mode",
    caption: "Collapsed Sidebar",
    category: "Dashboard",
  },
  {
    src: "/screenshots/user-menu.png",
    alt: "User menu dropdown with account settings and logout",
    caption: "User Menu",
    category: "Dashboard",
  },
  {
    src: "/screenshots/jobs-list.png",
    alt: "Jobs list view showing all configured file transfer jobs with status",
    caption: "Jobs List",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-create.png",
    alt: "Create new job form with name, description, and configuration options",
    caption: "Create Job",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-detail.png",
    alt: "Job detail view showing configuration, steps, schedules, and execution history",
    caption: "Job Details",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-steps.png",
    alt: "Job pipeline builder showing multi-step configuration with SFTP, PGP, and file operations",
    caption: "Job Steps Pipeline",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-executions.png",
    alt: "Job execution history showing past runs with status, duration, and step results",
    caption: "Execution History",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-schedules.png",
    alt: "Job scheduling interface with cron expression configuration",
    caption: "Job Schedules",
    category: "Jobs",
  },
  {
    src: "/screenshots/connections-list.png",
    alt: "Connections list showing SFTP, FTP, and local filesystem connections",
    caption: "Connections List",
    category: "Connections",
  },
  {
    src: "/screenshots/connection-create.png",
    alt: "Create connection form with protocol selection and credential configuration",
    caption: "Create Connection",
    category: "Connections",
  },
  {
    src: "/screenshots/connection-detail.png",
    alt: "Connection detail view showing configuration and test results",
    caption: "Connection Details",
    category: "Connections",
  },
  {
    src: "/screenshots/keys-pgp.png",
    alt: "PGP key management interface showing imported and generated keys",
    caption: "PGP Keys",
    category: "Keys",
  },
  {
    src: "/screenshots/keys-ssh.png",
    alt: "SSH key management interface for SFTP connection authentication",
    caption: "SSH Keys",
    category: "Keys",
  },
  {
    src: "/screenshots/pgp-key-detail.png",
    alt: "PGP key detail view with fingerprint, expiry, and usage information",
    caption: "PGP Key Details",
    category: "Keys",
  },
  {
    src: "/screenshots/pgp-key-generate.png",
    alt: "PGP key generation wizard with algorithm and key size options",
    caption: "Generate PGP Key",
    category: "Keys",
  },
  {
    src: "/screenshots/ssh-key-generate.png",
    alt: "SSH key generation wizard with key type and size options",
    caption: "Generate SSH Key",
    category: "Keys",
  },
  {
    src: "/screenshots/monitors-list.png",
    alt: "File monitors list showing directory watch configurations and status",
    caption: "Monitors List",
    category: "Monitoring",
  },
  {
    src: "/screenshots/monitor-create.png",
    alt: "Create file monitor form with directory path and trigger configuration",
    caption: "Create Monitor",
    category: "Monitoring",
  },
  {
    src: "/screenshots/monitor-detail.png",
    alt: "Monitor detail view showing watch configuration and triggered events",
    caption: "Monitor Details",
    category: "Monitoring",
  },
  {
    src: "/screenshots/admin-users.png",
    alt: "User management interface with role assignments and account status",
    caption: "User Management",
    category: "Admin",
  },
  {
    src: "/screenshots/admin-settings.png",
    alt: "System settings configuration panel",
    caption: "System Settings",
    category: "Admin",
  },
  {
    src: "/screenshots/audit-log.png",
    alt: "Audit log showing timestamped records of all user actions and system events",
    caption: "Audit Log",
    category: "Admin",
  },
  {
    src: "/screenshots/my-account.png",
    alt: "User account page with profile settings and password change",
    caption: "My Account",
    category: "Admin",
  },
  {
    src: "/screenshots/tags-page.png",
    alt: "Tag management for organizing jobs, connections, and other entities",
    caption: "Tags",
    category: "Admin",
  },
  {
    src: "/screenshots/chains-list.png",
    alt: "Job chains list showing linked job sequences for complex workflows",
    caption: "Chains List",
    category: "Chains",
  },
  {
    src: "/screenshots/chain-create.png",
    alt: "Create job chain form for linking multiple jobs in sequence",
    caption: "Create Chain",
    category: "Chains",
  },
  {
    src: "/screenshots/chain-detail.png",
    alt: "Chain detail view showing linked job sequence and execution flow",
    caption: "Chain Details",
    category: "Chains",
  },
  {
    src: "/screenshots/notifications-list.png",
    alt: "Notification configurations for job success and failure alerts",
    caption: "Notifications List",
    category: "Notifications",
  },
  {
    src: "/screenshots/notification-create.png",
    alt: "Create notification rule with event type and delivery method",
    caption: "Create Notification",
    category: "Notifications",
  },
];

export const categories: ScreenshotCategory[] = [
  "Dashboard",
  "Jobs",
  "Connections",
  "Keys",
  "Monitoring",
  "Admin",
  "Chains",
  "Notifications",
];
