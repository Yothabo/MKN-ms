import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  // Base path for the app (ensures correct routing on Vercel)
  base: '/',

  // Plugins for React, TypeScript paths, and PWA
  plugins: [
    react(),
    tsconfigPaths({ root: './' }),
    VitePWA({
      // Use injectManifest strategy to customize the service worker
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.js',
      injectManifest: {
        swSrc: './src/sw.js', // Source service worker file
        swDest: 'dist/sw.js', // Destination after build
        globPatterns: ['**/*.{js,css,html,png,svg}'], // Files to precache
        globIgnores: ['**/node_modules/**/*'], // Exclude node_modules
      },
      // Automatically inject the registration script
      injectRegister: 'auto',
      // Enable PWA in dev mode for testing
      devOptions: {
        enabled: true,
        type: 'module', // Use ES modules in dev
      },
      // PWA manifest configuration
      manifest: {
        name: 'Muzi Ka Nkulunkulu Management System',
        short_name: 'MKN-MS',
        theme_color: '#16a34a',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        scope: '/', // Defines the scope of the PWA
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
      // Optional: Workbox configuration for advanced control
      workbox: {
        globPatterns: ['**/*.{js,css,html,png,svg}'],
        cleanupOutdatedCaches: true,
        skipWaiting: true,
        clientsClaim: true,
      },
    }),
  ],

  // Resolve module extensions for TypeScript and JavaScript
  resolve: {
    extensions: ['.js', '.jsx', '.ts', '.tsx'],
  },

  // Server options (e.g., disable HMR overlay for errors)
  server: {
    hmr: {
      overlay: false, // Disable Vite's error overlay
    },
    open: true, // Automatically open browser on dev start
  },

  // Build configuration
  build: {
    chunkSizeWarningLimit: 1500, // Increase chunk size limit
    minify: 'esbuild', // Use esbuild for faster minification
    rollupOptions: {
      output: {
        manualChunks: undefined, // Disable manual chunking for simplicity
        entryFileNames: '[name]-[hash].js', // Unique filenames for cache busting
        chunkFileNames: '[name]-[hash].js',
        assetFileNames: '[name]-[hash].[ext]',
      },
    },
  },

  // Optimize dependencies for better performance
  optimizeDeps: {
    include: ['react', 'react-dom'],
  },
});
