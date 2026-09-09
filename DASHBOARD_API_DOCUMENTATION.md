# Dashboard API

**Base URL:** `/api/Dashboard`

**Response Wrapper:** The endpoint returns `ApiResponse<T>`:
```typescript
{ success: boolean; message: string; data: T | null; errors?: string[]; }
```

---

## 1. Recruiter Dashboard Quick View

`GET /api/Dashboard`

Single API for the main HR/recruiter dashboard. It is designed for a one-screen view that shows overall hiring load, urgent recruiter actions, pipeline health, source performance, aging requisitions, top candidates and recent activity.

Use this API for:
- Dashboard KPI cards
- Recruiter daily action list
- Pipeline charts
- Source performance charts
- Aging/open requisition tables
- Top candidate widgets
- Recent activity feed

**Status codes:** 200 OK | 400 Bad Request

---

## Query Params

All query params are optional.

| Parameter | Type | Example | Default | Meaning |
|---|---|---|---|---|
| `fromDate` | `yyyy-MM-dd` | `2026-06-01` | Today minus 30 days | Start date for date-range based metrics like new candidates and recent activity |
| `toDate` | `yyyy-MM-dd` | `2026-06-13` | Today | End date for date-range based metrics |
| `hrId` | number | `5` | null | Filters candidate/action sections to one recruiter using `Candidate.AssignedHRId` and activity owner data |

**Validation:**
- If `toDate` is earlier than `fromDate`, API returns `400 Bad Request`.
- If `hrId` does not match an existing user, API returns `400 Bad Request`.

---

## Example Calls

```http
GET /api/Dashboard
GET /api/Dashboard?fromDate=2026-06-01&toDate=2026-06-13
GET /api/Dashboard?hrId=5
GET /api/Dashboard?fromDate=2026-06-01&toDate=2026-06-13&hrId=5
```

---

## Response: `data` = `DashboardDto`

| Field | Type | Why it matters |
|---|---|---|
| `fromDate` / `toDate` | datetime | Exact range used by date-based dashboard blocks |
| `hrId` / `hrName` | nullable | Shows whether the dashboard is team-level or recruiter-level |
| `overview` | object | Main KPI cards: open jobs, candidates, offers, joins, conversion percentages |
| `pipeline` | object | Stage/status/priority/department breakdowns for charts |
| `recruiterActions` | object | The most important operational block: overdue follow-ups, today's follow-ups, upcoming interviews, stale candidates |
| `sourcePerformance` | array | Shows which source is producing candidates, shortlists, offers and joins |
| `agingRequisitions` | array | Oldest open hiring requests that need management attention |
| `topCandidates` | array | Highest-scored candidates based on latest evaluation score |
| `recentActivity` | array | Latest candidate movement/activity inside the selected date range |

---

## `overview` - `DashboardOverviewDto`

| Field | Type | Meaning |
|---|---|---|
| `totalHiringRequests` | number | Total active hiring requests in system |
| `openHiringRequests` | number | Requests not marked closed/cancelled/rejected/filled/completed |
| `pendingHrAcceptance` | number | Requests where HR status contains `pending` |
| `pendingDirectorApproval` | number | Requests where `isApprovedByDirector` is not true |
| `totalOpenPositions` | number | Sum of `numberOfPosition` for open requests |
| `totalCandidates` | number | Total active candidates in scope |
| `newCandidates` | number | Candidates created inside `fromDate` to `toDate` |
| `shortlistedCandidates` | number | Candidates shortlisted by date/stage/status |
| `offeredCandidates` | number | Candidates whose stage/status contains `offer` |
| `joinedCandidates` | number | Candidates whose stage/status contains `join` |
| `rejectedCandidates` | number | Candidates whose stage/status contains `reject` |
| `offerToJoinPercentage` | number | `joined / offered * 100` |
| `candidateToJoinPercentage` | number | `joined / totalCandidates * 100` |

---

## `pipeline` - `DashboardPipelineDto`

Each field is an array of:
```typescript
{ name: string; count: number; }
```

| Field | Meaning |
|---|---|
| `byStage` | Candidate count grouped by `currentStage` |
| `byStatus` | Candidate count grouped by `currentStatus` |
| `byPriority` | Open hiring request count grouped by priority |
| `byDepartment` | Open hiring request count grouped by department |

Use these for funnel charts, bar charts, or compact distribution widgets.

---

## `recruiterActions` - `DashboardRecruiterActionDto`

This is the most useful daily recruiter block.

| Field | Type | Meaning |
|---|---|---|
| `overdueFollowUps` | number | Active candidates whose `nextFollowUpDate` is before today |
| `dueTodayFollowUps` | number | Active candidates whose `nextFollowUpDate` is today |
| `upcomingFollowUps` | number | Active candidates with follow-ups in the next 7 days |
| `interviewsToday` | number | Interview activities dated today |
| `upcomingInterviews` | number | Scheduled/planned/upcoming interviews in the next 7 days |
| `staleCandidates` | number | Active candidates with no activity for 7+ days |
| `overdueFollowUpCandidates` | array | First 10 overdue follow-up candidates |
| `todayFollowUpCandidates` | array | First 10 candidates to call/follow up today |
| `upcomingInterviewDetails` | array | First 10 upcoming interview activities |

**Active candidates** exclude candidates whose stage/status contains `reject` or `join`.

---

## `sourcePerformance` - `DashboardSourceDto[]`

| Field | Meaning |
|---|---|
| `source` | Candidate source, e.g. LinkedIn, Naukri, Referral, Unspecified |
| `totalCandidates` | Candidates from this source |
| `shortlistedCandidates` | Shortlisted candidates from this source |
| `offeredCandidates` | Offered candidates from this source |
| `joinedCandidates` | Joined candidates from this source |

Use this to see which channel is producing quality, not just volume.

---

## `agingRequisitions` - `DashboardRequisitionDto[]`

Oldest open hiring requests, limited to top 10.

| Field | Meaning |
|---|---|
| `requestId` | Hiring request ID |
| `department` / `designation` | Role information |
| `priority` | Priority of hiring request |
| `hrStatus` | Current HR status |
| `numberOfPosition` | Number of openings |
| `candidateCount` | Candidates mapped to this request |
| `daysOpen` | Number of days since request was created |
| `createdAt` | Request creation date |
| `requiredByDate` | Target required-by date |

Use this table to identify stuck requisitions and urgent business risk.

---

## `topCandidates` - `DashboardCandidateDto[]`

Highest-scored candidates based on latest activity score, limited to top 10.

| Field | Meaning |
|---|---|
| `candidateId` | Candidate ID |
| `requisitionId` | Linked hiring request |
| `fullName`, `email`, `phoneNumber` | Candidate contact details |
| `source` | Candidate source |
| `currentStage` / `currentStatus` | Current candidate pipeline state |
| `assignedHRId` / `assignedHRName` | Recruiter owner |
| `nextFollowUpDate` | Next planned follow-up |
| `lastActivityDate` | Last candidate activity stored on candidate |
| `daysInPipeline` | Days since candidate was created |
| `latestScore` | Latest score from candidate activity |

---

## `recentActivity` / `upcomingInterviewDetails` - `DashboardActivityDto[]`

| Field | Meaning |
|---|---|
| `candidateId` / `candidateName` | Candidate involved |
| `requisitionId` | Linked hiring request |
| `activityType` | Activity type, e.g. Call, Interview, OfferReleased |
| `stage` | Stage at time of activity |
| `status` | Status at time of activity |
| `totalScore` | Optional evaluation score |
| `remarks` | Activity remarks |
| `actionDate` | Activity date/time |
| `performedById` / `performedByName` | User who performed/logged the activity |

---

## Example Response

```json
{
  "success": true,
  "message": "Dashboard data generated successfully.",
  "data": {
    "fromDate": "2026-06-01T00:00:00Z",
    "toDate": "2026-06-13T23:59:59.9999999Z",
    "hrId": 5,
    "hrName": "Anurag Sharma",
    "overview": {
      "totalHiringRequests": 25,
      "openHiringRequests": 14,
      "pendingHrAcceptance": 3,
      "pendingDirectorApproval": 2,
      "totalOpenPositions": 32,
      "totalCandidates": 180,
      "newCandidates": 36,
      "shortlistedCandidates": 48,
      "offeredCandidates": 9,
      "joinedCandidates": 5,
      "rejectedCandidates": 42,
      "offerToJoinPercentage": 55.56,
      "candidateToJoinPercentage": 2.78
    },
    "pipeline": {
      "byStage": [
        { "name": "applied", "count": 60 },
        { "name": "Interview", "count": 24 },
        { "name": "Offer", "count": 9 }
      ],
      "byStatus": [
        { "name": "pending", "count": 50 },
        { "name": "Scheduled", "count": 18 }
      ],
      "byPriority": [
        { "name": "High", "count": 8 },
        { "name": "Medium", "count": 6 }
      ],
      "byDepartment": [
        { "name": "Engineering", "count": 7 },
        { "name": "Sales", "count": 4 }
      ]
    },
    "recruiterActions": {
      "overdueFollowUps": 7,
      "dueTodayFollowUps": 5,
      "upcomingFollowUps": 12,
      "interviewsToday": 3,
      "upcomingInterviews": 8,
      "staleCandidates": 11,
      "overdueFollowUpCandidates": [],
      "todayFollowUpCandidates": [],
      "upcomingInterviewDetails": []
    },
    "sourcePerformance": [],
    "agingRequisitions": [],
    "topCandidates": [],
    "recentActivity": []
  }
}
```

---

## Angular TypeScript Interfaces

```typescript
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors?: string[];
}

export interface Dashboard {
  fromDate: string;
  toDate: string;
  hrId: number | null;
  hrName: string | null;
  overview: DashboardOverview;
  pipeline: DashboardPipeline;
  recruiterActions: DashboardRecruiterActions;
  sourcePerformance: DashboardSource[];
  agingRequisitions: DashboardRequisition[];
  topCandidates: DashboardCandidate[];
  recentActivity: DashboardActivity[];
}

export interface DashboardOverview {
  totalHiringRequests: number;
  openHiringRequests: number;
  pendingHrAcceptance: number;
  pendingDirectorApproval: number;
  totalOpenPositions: number;
  totalCandidates: number;
  newCandidates: number;
  shortlistedCandidates: number;
  offeredCandidates: number;
  joinedCandidates: number;
  rejectedCandidates: number;
  offerToJoinPercentage: number;
  candidateToJoinPercentage: number;
}

export interface DashboardPipeline {
  byStage: DashboardCount[];
  byStatus: DashboardCount[];
  byPriority: DashboardCount[];
  byDepartment: DashboardCount[];
}

export interface DashboardCount {
  name: string;
  count: number;
}

export interface DashboardRecruiterActions {
  overdueFollowUps: number;
  dueTodayFollowUps: number;
  upcomingFollowUps: number;
  interviewsToday: number;
  upcomingInterviews: number;
  staleCandidates: number;
  overdueFollowUpCandidates: DashboardCandidate[];
  todayFollowUpCandidates: DashboardCandidate[];
  upcomingInterviewDetails: DashboardActivity[];
}

export interface DashboardSource {
  source: string;
  totalCandidates: number;
  shortlistedCandidates: number;
  offeredCandidates: number;
  joinedCandidates: number;
}

export interface DashboardRequisition {
  requestId: number;
  department: string | null;
  designation: string | null;
  priority: string | null;
  hrStatus: string;
  numberOfPosition: number | null;
  candidateCount: number;
  daysOpen: number;
  createdAt: string;
  requiredByDate: string | null;
}

export interface DashboardCandidate {
  candidateId: number;
  requisitionId: number;
  fullName: string | null;
  email: string | null;
  phoneNumber: string | null;
  source: string | null;
  currentStage: string;
  currentStatus: string;
  assignedHRId: number | null;
  assignedHRName: string | null;
  nextFollowUpDate: string | null;
  lastActivityDate: string | null;
  daysInPipeline: number;
  latestScore: number | null;
}

export interface DashboardActivity {
  candidateId: number;
  candidateName: string | null;
  requisitionId: number | null;
  activityType: string;
  stage: string;
  status: string;
  totalScore: number | null;
  remarks: string | null;
  actionDate: string;
  performedById: number | null;
  performedByName: string | null;
}
```

---

## Angular Service Example

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse, Dashboard } from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private baseUrl = 'http://localhost:5000/api/Dashboard';

  constructor(private http: HttpClient) {}

  getDashboard(fromDate?: string, toDate?: string, hrId?: number): Observable<ApiResponse<Dashboard>> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    if (hrId) params = params.set('hrId', hrId);

    return this.http.get<ApiResponse<Dashboard>>(this.baseUrl, { params });
  }
}
```

---

## Recommended UI Layout

For one quick recruiter view:

1. Top KPI cards: open requests, open positions, total candidates, new candidates, shortlisted, offered, joined.
2. Red attention strip: overdue follow-ups, today follow-ups, interviews today, stale candidates.
3. Left chart: candidate pipeline by stage.
4. Right chart: open requisitions by priority.
5. Table: aging requisitions sorted by `daysOpen`.
6. Table: today and overdue follow-up candidates.
7. Table/widget: top candidates by latest score.
8. Feed: recent activity.

This gives management and recruiters the same page: workload, urgency, quality, and movement.
