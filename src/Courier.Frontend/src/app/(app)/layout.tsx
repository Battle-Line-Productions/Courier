import { Shell } from "@/components/layout/shell";
import { AuthGuard } from "@/components/auth/auth-guard";

export default function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <AuthGuard>
      <Shell>{children}</Shell>
    </AuthGuard>
  );
}
