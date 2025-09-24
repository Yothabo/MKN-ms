import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig({
  plugins: [react(), tsconfigPaths({ root: './' })],
  resolve: {
    extensions: ['.js', '.jsx', '.ts', '.tsx'],
  },
  server: {
    hmr: {
      overlay: false, // Disable HMR overlay for cleaner error logs
    },
  },
});
