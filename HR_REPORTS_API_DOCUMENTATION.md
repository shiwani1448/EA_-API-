# HR Reports API

**Base URL:** `/api/HrReports`

**Response Wrapper:** Both endpoints return `ApiResponse<T>`:
```typescript
{ success: boolean; message: string; data: T | null; errors?: string[]; }
```

---

## 1. Hiring Request Report

`GET /api/HrReports/hiring-request/{requestId}`

Full HR activity report for one requisition — use this on a "Requisition Detail" screen.

**Status codes:** 200 OK | 404 Not Found (requisition doesn't exist)

### Response: `data` = `HiringRequestReportDto`

| Field | Type | Why it matters |
|---|---|---|
| `requestId`, `department`, `designation`, `numberOfPosition`, `priority`, `hrStatus` | basic info | Identify which requisition this is, for the page header |
| `requestRaisedAt` / `hrAcceptedAt` | datetime | Raw timestamps of when the request was raised vs accepted |
| `hrResponseTime` | object | **TAT (turnaround time)** — how fast HR picked up the request. `isAccepted` (bool), `hoursToAccept` (number), `formattedDuration` ("1d 4h 30m"). Use this for an SLA/speed widget |
| `sourcing` | object | All sourcing effort for this requisition (see **Sourcing block** below) |
| `candidatePipeline` | object | Funnel of every candidate sourced for this requisition (see **Pipeline block** below) |
| `callFollowUps` | object | How many calls/follow-ups HR made for this requisition's candidates |
| `interviews` | object | Interviews scheduled vs. actually conducted |
| `candidateScores` | object | Evaluation scores given to candidates (screening/interview) |
| `activityTimeline` | array | **The master log** — every action taken on every candidate, newest first. Use this for an audit/history tab |

---

## 2. Weekly HR Report

`GET /api/HrReports/weekly?weekStartDate=2026-06-08&hrId=12`

Org-wide (or per-recruiter) snapshot of everything that happened in a calendar week. Use this for a "Weekly Performance" dashboard.

**Query params (both optional):**
- `weekStartDate` — any `yyyy-MM-dd` date inside the target week. The API snaps it to that week's **Monday**. If omitted, defaults to the current week.
- `hrId` — User ID of a recruiter. Narrows `newCandidates`, `callFollowUps`, `interviews`, `candidateScores`, `dailyBreakdown`, and `activityTimeline` to that recruiter's work (matched via `Candidate.AssignedHRId` / `CandidateActivity.CreatedBy`).
  - ⚠️ `sourcing` and `requestsAccepted` are **always org-wide** — those tables have no per-HR owner column.

**Status codes:** 200 OK | 400 Bad Request (`hrId` doesn't match a user)

### Response: `data` = `WeeklyHrReportDto`

| Field | Type | Why it matters |
|---|---|---|
| `weekStart` / `weekEnd` | datetime | The exact Mon 00:00 → Sun 23:59:59 range the report covers |
| `hrId` / `hrName` | nullable | Echoes back the recruiter filter (null = whole team) |
| `requestsAccepted` | array | Every requisition HR **accepted this week**, with `hoursToAccept` — your "speed of response" leaderboard data |
| `sourcing` | object | All sourcing tasks **created this week**, across all requisitions |
| `newCandidates` | object | Candidates **added this week** ("how many candidates came in") — same shape as the pipeline block |
| `shortlistedThisWeek` | number | Count of candidates whose `shortlistingDate` falls in this week (regardless of when they were added) |
| `callFollowUps` | object | Calls/follow-ups logged this week |
| `interviews` | object | Interviews scheduled/conducted this week |
| `candidateScores` | object | All scores recorded this week (avg/high/low) |
| `dailyBreakdown` | array | 7 entries (Mon→Sun), each with `date`, `totalActivities`, `callsAndFollowUps`, `interviews`, `shortlisted` — feeds a bar/line chart of the week |
| `activityTimeline` | array | Every activity logged this week, newest first |

---

## Shared blocks (used in both reports)

### `sourcing` — `SourcingSummaryDto`
| Field | Meaning |
|---|---|
| `totalTasks` / `completedTasks` / `pendingTasks` / `inProgressTasks` | Top-line sourcing KPIs |
| `byChannel` | Array of `{ channel, totalTasks, completedTasks, pendingTasks }` — **shows where candidates are being sourced from** (Naukri vs LinkedIn vs Referral etc.) and how productive each channel is |
| `tasks` | Full list of `SourcingTaskDto` — each task's `source`, `subSource`, `status`, `startTime`/`endTime`, `durationHours`, and the raw **`taskDetails` JSON** (whatever custom fields were captured for that sourcing task) |

### `candidatePipeline` / `newCandidates` — `CandidatePipelineSummaryDto`
| Field | Meaning |
|---|---|
| `totalCandidates` | Total candidates in scope |
| `shortlistedCount` / `rejectedCount` / `offeredCount` / `joinedCount` | End-state funnel counts |
| `byStage` / `byStatus` | Array of `{ name, count }` — **funnel breakdown**, drives a stage-wise bar chart (applied → screening → shortlisted → offer → joined, etc.) |
| `candidates` | Lightweight candidate list `{ candidateId, fullName, source, currentStage, currentStatus, createdAt, shortlistingDate }` for a table view |

### `callFollowUps` — `CallFollowUpSummaryDto`
| Field | Meaning |
|---|---|
| `totalCallsAndFollowUps` | Count of activity log entries classified as a call/follow-up |
| `pendingFollowUps` | Candidates with a **future** `nextFollowUpDate` — "calls HR still owes" |
| `details` | The matching `activityTimeline` entries, for drill-down |

### `interviews` — `InterviewSummaryDto`
| Field | Meaning |
|---|---|
| `totalScheduled` | Interview activities whose status looks like "scheduled/planned/upcoming" |
| `totalCompleted` | Remaining interview activities — i.e., interviews that actually **happened** |
| `details` | The matching activity entries |

### `candidateScores` — `CandidateScoreSummaryDto`
| Field | Meaning |
|---|---|
| `averageScore` / `highestScore` / `lowestScore` | Aggregate quality of candidates evaluated |
| `scores` | Per-evaluation `{ candidateId, candidateName, stage, totalScore, actionDate }` — "how candidates scored" in detail |

### `activityTimeline` item — `ActivityTimelineItemDto`
The atomic unit everything else is derived from:
```typescript
{
  candidateId: number;
  candidateName: string | null;
  requisitionId: number | null;
  activityType: string;   // e.g. "Call", "Interview", "OfferReleased"
  stage: string;          // e.g. "screening", "Interview", "Offer"
  status: string;         // e.g. "Scheduled", "Completed", "Shortlisted"
  totalScore: number | null;
  remarks: string | null;
  actionDate: string;     // ISO datetime
  performedBy: string | null; // resolved HR/recruiter name
}
```
Use this for any "history" or "audit trail" view — every other count/summary is just a filtered/grouped view of this list.

---

## Angular TypeScript interfaces

```typescript
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors?: string[];
}

export interface ActivityTimelineItem {
  candidateId: number;
  candidateName: string | null;
  requisitionId: number | null;
  activityType: string;
  stage: string;
  status: string;
  totalScore: number | null;
  remarks: string | null;
  actionDate: string;
  performedBy: string | null;
}

export interface StageCount { name: string; count: number; }

export interface CandidateSummary {
  candidateId: number;
  fullName: string | null;
  source: string | null;
  currentStage: string;
  currentStatus: string;
  createdAt: string;
  shortlistingDate: string | null;
}

export interface CandidatePipelineSummary {
  totalCandidates: number;
  shortlistedCount: number;
  rejectedCount: number;
  offeredCount: number;
  joinedCount: number;
  byStage: StageCount[];
  byStatus: StageCount[];
  candidates: CandidateSummary[];
}

export interface SourcingTask {
  sourcingId: number;
  hiringRequestId: number;
  source: string | null;
  subSource: string | null;
  status: string;
  startTime: string | null;
  endTime: string | null;
  durationHours: number | null;
  taskDetails: Record<string, any> | null;
  createdAt: string;
}

export interface SourcingChannelBreakdown {
  channel: string;
  totalTasks: number;
  completedTasks: number;
  pendingTasks: number;
}

export interface SourcingSummary {
  totalTasks: number;
  completedTasks: number;
  pendingTasks: number;
  inProgressTasks: number;
  byChannel: SourcingChannelBreakdown[];
  tasks: SourcingTask[];
}

export interface CallFollowUpSummary {
  totalCallsAndFollowUps: number;
  pendingFollowUps: number;
  details: ActivityTimelineItem[];
}

export interface InterviewSummary {
  totalScheduled: number;
  totalCompleted: number;
  details: ActivityTimelineItem[];
}

export interface CandidateScore {
  candidateId: number;
  candidateName: string | null;
  stage: string;
  totalScore: number;
  actionDate: string;
}

export interface CandidateScoreSummary {
  averageScore: number | null;
  highestScore: number | null;
  lowestScore: number | null;
  scores: CandidateScore[];
}

export interface HrResponseTime {
  isAccepted: boolean;
  hoursToAccept: number | null;
  formattedDuration: string | null;
}

export interface HiringRequestReport {
  requestId: number;
  department: string | null;
  designation: string | null;
  numberOfPosition: number | null;
  priority: string | null;
  hrStatus: string;
  requestRaisedAt: string;
  hrAcceptedAt: string | null;
  hrResponseTime: HrResponseTime;
  sourcing: SourcingSummary;
  candidatePipeline: CandidatePipelineSummary;
  callFollowUps: CallFollowUpSummary;
  interviews: InterviewSummary;
  candidateScores: CandidateScoreSummary;
  activityTimeline: ActivityTimelineItem[];
}

export interface HrAcceptance {
  requestId: number;
  department: string | null;
  designation: string | null;
  requestRaisedAt: string;
  hrAcceptedAt: string;
  hoursToAccept: number;
}

export interface DailyActivityCount {
  date: string; // yyyy-MM-dd
  totalActivities: number;
  callsAndFollowUps: number;
  interviews: number;
  shortlisted: number;
}

export interface WeeklyHrReport {
  weekStart: string;
  weekEnd: string;
  hrId: number | null;
  hrName: string | null;
  requestsAccepted: HrAcceptance[];
  sourcing: SourcingSummary;
  newCandidates: CandidatePipelineSummary;
  shortlistedThisWeek: number;
  callFollowUps: CallFollowUpSummary;
  interviews: InterviewSummary;
  candidateScores: CandidateScoreSummary;
  dailyBreakdown: DailyActivityCount[];
  activityTimeline: ActivityTimelineItem[];
}
```

## Angular service

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse, HiringRequestReport, WeeklyHrReport } from './hr-report.models';

@Injectable({ providedIn: 'root' })
export class HrReportService {
  private baseUrl = 'http://localhost:5000/api/HrReports';

  constructor(private http: HttpClient) {}

  getHiringRequestReport(requestId: number): Observable<ApiResponse<HiringRequestReport>> {
    return this.http.get<ApiResponse<HiringRequestReport>>(`${this.baseUrl}/hiring-request/${requestId}`);
  }

  getWeeklyReport(weekStartDate?: string, hrId?: number): Observable<ApiResponse<WeeklyHrReport>> {
    let params = new HttpParams();
    if (weekStartDate) params = params.set('weekStartDate', weekStartDate); // 'yyyy-MM-dd'
    if (hrId) params = params.set('hrId', hrId);
    return this.http.get<ApiResponse<WeeklyHrReport>>(`${this.baseUrl}/weekly`, { params });
  }
}
```

## Example calls
```
GET /api/HrReports/hiring-request/15
GET /api/HrReports/weekly                              # current week, whole team
GET /api/HrReports/weekly?weekStartDate=2026-06-08     # specific week, whole team
GET /api/HrReports/weekly?weekStartDate=2026-06-08&hrId=101   # specific week, one recruiter
```
