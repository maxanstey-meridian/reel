import { mountSuspended } from "@nuxt/test-utils/runtime";
import { describe, expect, it } from "vitest";
import MediaThumbnail from "~~/app/components/MediaThumbnail.vue";
import type { FileNode } from "~~/app/lib/api";

const file = (relativePath: string, kind: "video" | "audio"): FileNode => ({
  name: relativePath,
  type: "file",
  kind,
  relativePath,
});

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

  it("shows a folder's first video", async () => {
    const folder: FileNode = {
      name: "Mixed",
      type: "directory",
      relativePath: "Mixed",
      children: [file("Mixed/Theme.mp3", "audio"), file("Mixed/Episode.mp4", "video")],
    };

    expect(await thumbnailOf(folder)).toBe("Mixed/Episode.mp4");
  });
});
