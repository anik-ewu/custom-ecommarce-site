export interface User {
  id: string;
  email: string;
  name: string;
  role: 'Customer' | 'Admin';
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}
