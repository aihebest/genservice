import { PublicClientApplication, type Configuration, LogLevel } from '@azure/msal-browser';

const msalConfig: Configuration = {
  auth: {
    clientId:              import.meta.env.VITE_ENTRA_CLIENT_ID || '',
    authority:             `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID || 'common'}`,
    // Redirect to /login directly so React Router never strips the ?code= params.
    // The root (origin) causes a client-side redirect to /login which loses the auth code.
    redirectUri:           `${window.location.origin}/login`,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
  system: {
    loggerOptions: {
      loggerCallback: (_level, message, containsPii) => {
        if (!containsPii && _level === LogLevel.Error) console.error('[MSAL]', message);
      },
      logLevel: LogLevel.Warning,
    },
  },
};

export const msalInstance = new PublicClientApplication(msalConfig);

export const loginRequest = {
  scopes: ['openid', 'email', 'profile'],
};
