# Toggl2Vertec

One-way synchronization of tracked work time from Toggl into Vertec, aggregated per day and per Vertec project.

## Language

### Source (Toggl)

**Toggl**:
The time-tracking source, specifically the Toggl 2.0 (Focus) product and its API.
_Avoid_: Toggl Track, Reports API

**Time Entry**:
One tracked activity in Toggl with a start, a duration and a text; breaks and planned-only entries are not Time Entries.
_Avoid_: time block, log entry

**Project**:
The Toggl project a Time Entry belongs to; its name carries the Vertec phase ID.
_Avoid_: task

### Day model

**Working Day**:
All tracked time of one user for one calendar date, aggregated into Summaries and Attendance before being written to Vertec.
_Avoid_: day data, day entries

**Empty Day**:
A date for which Toggl has no tracked time; it is never written to Vertec.

### Vertec operations

**Update**:
Writing a Working Day into Vertec, overwriting only the Vertec entries for which Toggl has corresponding data.
_Avoid_: sync, push

**Clear**:
Removing all of the user's attendance and work entries in Vertec for a date.
_Avoid_: delete, reset

**Force Update**:
A Clear followed by an Update of the same date, so Vertec ends up containing exactly what Toggl has.
_Avoid_: overwrite, hard update

**Batch Update**:
An Update (or Force Update) over a contiguous date range, skipping Empty Days.
_Avoid_: bulk update, range sync

**Scheduled Day**:
A date on which Vertec expects work from the user (target time above zero); weekends and public holidays are not.
_Avoid_: working day (that is the Toggl-side aggregate)

**Unfilled Day**:
A Scheduled Day with no recorded time at all; vacation and absences count as recorded time.

**First Unfilled Day**:
The earliest Unfilled Day of the current month, up to today.

**Catch-up**:
An Update of every Unfilled Day from the First Unfilled Day to today, confirmed by the user before anything is written; days that already have recorded time are never touched.
_Avoid_: auto sync

**Validated Month**:
A past calendar month that Vertec has locked; no date in it may be Updated or Cleared.
