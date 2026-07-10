import { logoutAction } from "@/app/dashboard/actions";
import { Button } from "@/components/ui/button";

export function LogoutForm() {
  return (
    <form action={logoutAction}>
      <Button type="submit" variant="outline">
        Cerrar sesion
      </Button>
    </form>
  );
}
