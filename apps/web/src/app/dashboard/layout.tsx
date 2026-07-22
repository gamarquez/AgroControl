import { DashboardNav } from "@/components/dashboard/dashboard-nav";
import { requireSession } from "@/lib/auth/session";

export default async function DashboardLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const session = await requireSession();
  return (
    <div className="min-h-screen bg-background lg:pl-64">
      <DashboardNav displayName={session.displayName} email={session.email} />
      <main className="mx-auto max-w-[1480px] px-4 py-6 sm:px-6 lg:px-10 lg:py-9">{children}</main>
    </div>
  );
}
