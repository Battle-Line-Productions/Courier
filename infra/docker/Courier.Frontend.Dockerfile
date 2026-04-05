# =============================================================================
# Courier Frontend — Multi-stage Docker Build (Next.js Standalone)
# =============================================================================
# Build:  docker build -f infra/docker/Courier.Frontend.Dockerfile \
#           --build-arg NEXT_PUBLIC_API_URL=https://api.courier.example.com \
#           -t courier-frontend .
# Run:    docker run -p 3000:3000 courier-frontend
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Install dependencies
# ---------------------------------------------------------------------------
FROM node:24-alpine AS deps
WORKDIR /app

COPY src/Courier.Frontend/package.json src/Courier.Frontend/package-lock.json ./
RUN npm ci

# ---------------------------------------------------------------------------
# Stage 2: Build
# ---------------------------------------------------------------------------
FROM node:24-alpine AS build
WORKDIR /app

COPY --from=deps /app/node_modules ./node_modules
COPY src/Courier.Frontend/ .

# NEXT_PUBLIC_* vars are baked into the JS bundle at build time.
# Rebuild the frontend image per environment with the correct API URL.
ARG NEXT_PUBLIC_API_URL=http://localhost:5000
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}

RUN npm run build

# ---------------------------------------------------------------------------
# Stage 3: Runtime (standalone Node.js server)
# ---------------------------------------------------------------------------
FROM node:24-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

# Copy the standalone server output
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

EXPOSE 3000

# Alpine has wget, not curl
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:3000 || exit 1

CMD ["node", "server.js"]
