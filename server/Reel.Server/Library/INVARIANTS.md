# Library invariants

## LIB-INDEX-001 A rescan never shrinks the index on a doubtful scan

**Rule:** A rescan changes nothing when the scan could not read every folder, or when it found no media while the index still holds some.
**Why:** Deleting media rows cascades to progress and favourites; an unmounted or unreadable drive must not wipe them.
**Accepts:** Removing every file from the library leaves the index as it was until something is added back.
**Owner:** `LibraryIndexReconciler.ReconcileAsync`
**Proof:** `IncompleteScan_PreservesMediaProgressAndFavouritesWithoutPartialAdditions`, `CompletedScanFindingNothing_PreservesNonEmptyIndexAndProgress`

## LIB-INDEX-002 Rows indexed after a scan started survive that scan

**Rule:** Reconciling deletes only rows that are missing from the scan and were indexed before the scan started; files indexed on demand during a scan (by playing or favouriting them) stay.
**Why:** A long scan that passed a folder before a new file arrived would otherwise delete that file's progress and favourites.
**Owner:** `SqliteMediaStore.ReconcileAsync`
**Proof:** `ReconcileAsync_KeepsRowsIndexedAfterTheScanStartedAndRemovesOlderMissingRows`

## LIB-PATH-001 A path resolves only if a scan would list it

**Rule:** Request paths resolve to a file only by walking real directory entries under the media root with the scanner's own rules (no hidden entries, no symlinks, playable extensions, exact names).
**Why:** Keeps the media-root boundary and stops a request reaching files the library never shows.
**Owner:** `MediaPathResolver.ResolvePlayableFile`
**Proof:** `Resolve_TraversalToExistingFileOutsideRoot_ReturnsNull`, `Resolve_EntriesTheScannerSkips_ReturnNull`

## LIB-TAGS-001 #media is a removable default for untagged files

**Rule:** A file with no stored tags counts as `#media`; a stored empty set means the user removed it. Folders have no default, and their tags apply to every file inside.
**Why:** Saving `#media` on a folder would tag every file in it, including files whose own tags were emptied.
**Owner:** `SqliteMediaStore.SearchAsync` (search) and `useTags.forPath` in the web app (what the editor saves)
**Proof:** `DefaultMediaTagIsSearchableButRemovedTagStaysRemoved`; web `useTags` test "gives an untagged folder no tags, so tagging it doesn't tag every file #media"
