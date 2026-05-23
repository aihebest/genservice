import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser } from '../types';

interface AuthState {
  token:   string | null;
  user:    AuthUser | null;
  isAuth:  boolean;
  setAuth: (token: string, user: AuthUser) => void;
  logout:  () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token:  null,
      user:   null,
      isAuth: false,

      setAuth: (token, user) => set({ token, user, isAuth: true }),

      logout: () => set({ token: null, user: null, isAuth: false }),
    }),
    {
      name:    'gs-auth',          // localStorage key
      partialize: (state) => ({    // only persist token + user, not functions
        token:  state.token,
        user:   state.user,
        isAuth: state.isAuth,
      }),
    }
  )
);
