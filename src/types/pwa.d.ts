// src/types/pwa.d.ts
declare module 'virtual:pwa-register/react' {
  import { Dispatch, SetStateAction } from 'react';

  export function useRegisterSW(): {
    needRefresh: [boolean, Dispatch<SetStateAction<boolean>>];
    offlineReady: [boolean, Dispatch<SetStateAction<boolean>>];
    updateServiceWorker: (reload?: boolean) => Promise<void>;
  };
}

declare module 'virtual:pwa-register' {
  export function registerSW(options?: {
    onNeedRefresh?: () => void;
    onOfflineReady?: () => void;
    onRegistered?: (registration: ServiceWorkerRegistration) => void;
    onRegisterError?: (error: any) => void;
  }): () => Promise<void>;
}
