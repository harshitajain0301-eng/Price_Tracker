import "dotenv/config";
import express from "express";
import cors from "cors";
import { connectDb } from "./db.js";
import { createRouter } from "./routes.js";
import { startPriceChecker } from "./priceChecker.js";

const PORT = process.env.PORT || 4000;
const MONGODB_URI = process.env.MONGODB_URI || "mongodb://127.0.0.1:27017/smart_price_intel";
const CLIENT_ORIGIN = process.env.CLIENT_ORIGIN || "http://localhost:5173";
const SERPAPI_KEY = process.env.SERPAPI_KEY || "";

async function main() {
  await connectDb(MONGODB_URI);
  console.log("MongoDB connected");

  const app = express();
  app.use(
    cors({
      origin: CLIENT_ORIGIN,
      credentials: true,
    })
  );
  app.use(express.json({ limit: "1mb" }));

  app.get("/health", (_, res) => res.json({ ok: true }));

  app.use("/api", createRouter({ serpApiKey: SERPAPI_KEY }));

  const stopChecker = startPriceChecker(SERPAPI_KEY);

  const server = app.listen(PORT, () => {
    console.log(`API http://localhost:${PORT}`);
  });

  const shutdown = () => {
    stopChecker();
    server.close(() => process.exit(0));
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
