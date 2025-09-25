import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { registerSW } from 'virtual:pwa-register';
import App from './App';
import './index.css';

// Register the service worker
registerSW({
  onOfflineReady() {
    console.log('App ready for offline use');

    // Restore last route after SW registration
    const last = localStorage.getItem('lastRoute');
    if (last && last !== window.location.pathname) {
      window.history.replaceState({}, '', last);
    }
  },
  onNeedRefresh() {
    console.log('New version available');
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>
);
