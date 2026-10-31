# StageLink web app

The Angular frontend of [StageLink](../README.md).

```bash
npm install
npm start                      # http://localhost:4200 (needs the backend on :5080)
npx ng test --watch=false      # unit tests (Vitest)
npx ng build                   # production build in dist/
```

During development, `proxy.conf.json` forwards `/api` and `/hubs` to the backend at `http://localhost:5080`.

## Structure

```
src/app/
  core/          app-wide services: backend status, HTTP API, SignalR connection
  speakers/      speaker-list, speaker-page, control-panel, telemetry-panel
  testing/       fakes used only by tests (excluded from the production build)
```
