import { mountSuspended } from "@nuxt/test-utils/runtime";
import { describe, expect, it } from "vitest";
import MediaThumbnail from "~~/app/components/MediaThumbnail.vue";
import type { FileNode } from "~~/app/lib/api";
import { fileNode } from "../support/fixtures";

const file = (relativePath: string, kind: "video" | "audio"): FileNode =>
  fileNode({ name: relativePath, type: "file", kind, relativePath });

const thumbnailOf = async (node: FileNode) => {
  const thumbnail = await mountSuspended(MediaThumbnail, { props: { node } });
  const image = thumbnail.find("img");
  return image.exists()
    ? new URL(image.attributes("src")!, "http://reel").searchParams.get("path")
    : null;
};

describe("MediaThumbnail", () => {
  it("shows a frame for video only", async () => {
    expect(await thumbnailOf(file("Film.mkv", "video"))).toBe("Film.mkv");
    expect(await thumbnailOf(file("Song.flac", "audio"))).toBeNull();
  });

  it("marks audio, video and folders with their own icon until a frame loads", async () => {
    const iconOf = async (node: FileNode) =>
      (await mountSuspended(MediaThumbnail, { props: { node } }))
        .get(".iconify")
        .classes()
        .find((name) => name.startsWith("i-lucide:"));

    expect(await iconOf(file("Song.flac", "audio"))).toBe("i-lucide:music");
    expect(await iconOf(file("Film.mkv", "video"))).toBe("i-lucide:film");
    expect(
      await iconOf(
        fileNode({ name: "Shows", type: "directory", relativePath: "Shows", children: [] }),
      ),
    ).toBe("i-lucide:folder");
  });

  it("shows a folder's first video", async () => {
    const folder = fileNode({
      name: "Mixed",
      type: "directory",
      relativePath: "Mixed",
      children: [file("Mixed/Theme.mp3", "audio"), file("Mixed/Episode.mp4", "video")],
    });

    expect(await thumbnailOf(folder)).toBe("Mixed/Episode.mp4");
  });
});
