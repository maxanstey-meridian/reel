# Playback invariants

## PLAY-ORDER-001 TV reports never move backwards

**Rule:** Each observation's sequence number is higher than the last one stored (an exact retry of the last one is accepted once, unchanged), and its applied revision is never lower than the stored one nor higher than the session's.
**Why:** Reports cross each other on the network; an older report must never overwrite what the TV last said.
**Owner:** `PlaybackSessionService.ReportObservation`
**Proof:** `Observation_RejectsReorderedSequenceAtEqualRevision`, `Observation_RejectsAppliedRevisionRegressionWithoutChangingStoredObservation`, `Observation_ExactSequenceRetryIsIdempotentWithoutRefreshingFreshness`

## PLAY-SEEK-001 A seek stays pending until the TV acknowledges its id

**Rule:** A requested seek stays on the intent until an observation reports that seek's id as applied; seek ids only increase within a session.
**Why:** The phone shows the requested position until the TV has actually moved there.
**Owner:** `PlaybackSessionService.UpdateIntent` and `ReportObservation`
**Proof:** `Observation_RecordsActualStateAndClearsAcknowledgedPendingSeek`, `UpdateIntent_ReplacesPendingSeekAndKeepsSeekIdsMonotonicAfterAcknowledgement`

## PLAY-EXPIRY-001 A finished or stopped session ends 30 seconds after the change

**Rule:** Once stopped or ended, a session expires 30 seconds after its last intent change or change of observed state; the TV's routine once-a-second reports don't extend it.
**Why:** Otherwise a stopped title stays on the phone for as long as the TV is on.
**Accepts:** The TV's "Play next" prompt clears when the session expires.
**Owner:** `PlaybackSessionService.ExpireIfNeeded`
**Proof:** `StoppedSession_ExpiresThirtySecondsAfterTheTvStopsEvenWhileItKeepsReporting`
