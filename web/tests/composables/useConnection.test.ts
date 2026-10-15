import { beforeEach, describe, expect, it } from "vitest";
import { client, thumbnailUrl } from "~~/app/lib/api";
import { fakeServer } from "../support/fakeServer";

const rejectsWrongCode = (request: Request) =>
  request
    .json()
    .then(({ key }: { key: string }) =>
      key === "secret"
        ? Response.json({ status: "connected" })
        : Response.json(
            { code: "unauthorized", message: "That code doesn't match your TV." },
            { status: 401 },
          ),
    );

describe("useConnection", () => {
  beforeEach(() => {
    useConnection().disconnect();
  });

  it("pairs with a code the server accepts and uses it from then on", async () => {
    const server = fakeServer({
      "POST /api/connect": rejectsWrongCode,
      "GET /api/library": () => Response.json(null),
    });
    const { connect, isConnected } = useConnection();

    await connect("secret");
    await client.GET("/api/library");

    expect(isConnected.value).toBe(true);
    expect(server.requests("GET /api/library")[0]?.headers.get("X-Secret-Key")).toBe("secret");
    expect(thumbnailUrl("a.mkv")).toContain("token=secret");
  });

  it("explains a wrong code and keeps the working pairing", async () => {
    fakeServer({ "POST /api/connect": rejectsWrongCode });
    const { connect, isConnected } = useConnection();
    await connect("secret");

    await expect(connect("wrong")).rejects.toThrow("That code doesn't match your TV.");
    expect(isConnected.value).toBe(true);
    expect(thumbnailUrl("a.mkv")).toContain("token=secret");
  });

  it("forgets the pairing on disconnect", async () => {
    fakeServer({ "POST /api/connect": rejectsWrongCode });
    const { connect, disconnect, isConnected } = useConnection();
    await connect("secret");

    disconnect();

    expect(isConnected.value).toBe(false);
  });
});
