// SPDX-License-Identifier: LGPL-3.0-or-later

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Barkeep during development; production serving is the #6 pairing/transport unit.
      "/api": "http://localhost:5273",
      "/ws": { target: "ws://localhost:5273", ws: true },
    },
  },
})
