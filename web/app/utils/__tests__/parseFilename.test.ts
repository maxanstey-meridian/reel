import { describe, it, expect } from "vitest";
import { parseFilename } from "../parseFilename";

describe("parseFilename", () => {
  it("parses dotted filename with year and tags", () => {
    expect(parseFilename("The.Matrix.1999.1080p.BluRay.x264.mp4")).toEqual({
      title: "The Matrix",
      year: 1999,
    });
  });

  it("parses parenthesised year", () => {
    expect(parseFilename("Inception (2010).mkv")).toEqual({
      title: "Inception",
      year: 2010,
    });
  });

  it("strips tags without a year", () => {
    expect(parseFilename("movie_name_720p_WEBRip.mp4")).toEqual({
      title: "movie name",
      year: null,
    });
  });

  it("preserves clean filenames", () => {
    expect(parseFilename("A Simple File.mp4")).toEqual({
      title: "A Simple File",
      year: null,
    });
  });

  it("handles year-like number in title (2001 A Space Odyssey)", () => {
    expect(parseFilename("2001.A.Space.Odyssey.1968.REMUX.mkv")).toEqual({
      title: "2001 A Space Odyssey",
      year: 1968,
    });
  });

  it("falls back gracefully for degenerate filenames", () => {
    const result = parseFilename(".mp4");
    expect(result.title).toBeTruthy();
    expect(result.year).toBeNull();
  });

  it("strips multiple codec and quality tags", () => {
    expect(
      parseFilename("No.Country.for.Old.Men.2007.x265.HEVC.10bit.AAC.mkv"),
    ).toEqual({
      title: "No Country for Old Men",
      year: 2007,
    });
  });

  it("handles WEB-DL as a single tag", () => {
    expect(parseFilename("Movie.2020.WEB-DL.1080p.mp4")).toEqual({
      title: "Movie",
      year: 2020,
    });
  });

  it("handles filename with no extension", () => {
    expect(parseFilename("The.Movie.2020")).toEqual({
      title: "The Movie",
      year: 2020,
    });
  });

  it("handles underscored filename with year", () => {
    expect(parseFilename("Blade_Runner_2049_2017_UHD.mp4")).toEqual({
      title: "Blade Runner 2049",
      year: 2017,
    });
  });
});
