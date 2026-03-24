export interface AuthTokenResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export interface AuthRegisterRequest {
  login: string;
  email: string;
  password: string;
}

export interface AuthLoginRequest {
  login: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface AuthUser {
  userId: string;
  userName: string;
  email: string;
}
