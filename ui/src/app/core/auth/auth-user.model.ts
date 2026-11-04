export interface AuthUser {
  sub: string;
  name?: string;
  email?: string;
  phoneNumber?: string;
  roles: string[];
  permissions: string[];
  scopes: string[];
}
