import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  base: '/',
  
  plugins: [
    react({
      jsxRuntime: 'automatic',
    }),
    tsconfigPaths({ root: './' }),
    VitePWA({
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.js',
      injectManifest: {
        swSrc: './src/sw.js',
        swDest: 'dist/sw.js',
        globPatterns: ['**/*.{js,css,html,png,svg}'],
        globIgnores: ['**/node_modules/**/*'],
      },
      injectRegister: 'auto',
      devOptions: {
        enabled: true,
        type: 'module',
      },
      manifest: {
        name: 'Muzi Ka Nkulunkulu Management System',
        short_name: 'MKN-MS',
        theme_color: '#16a34a',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          {
            src: '/icons/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
          },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,png,svg}'],
        cleanupOutdatedCaches: true,
        skipWaiting: true,
        clientsClaim: true,
      },
    }),
  ],

  resolve: {
    extensions: ['.js', '.jsx', '.ts', '.tsx'],
  },

  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: false,
    hmr: {
      overlay: false,
    },
    open: true,
  },

  build: {
    sourcemap: false,
    chunkSizeWarningLimit: 1500,
    minify: 'esbuild',
    rollupOptions: {
      output: {
        manualChunks: undefined,
        entryFileNames: '[name]-[hash].js',
        chunkFileNames: '[name]-[hash].js',
        assetFileNames: '[name]-[hash].[ext]',
      },
    },
  },

  optimizeDeps: {
    include: ['react', 'react-dom'],
    exclude: ['react-svg-worldmap'],
  },
});
