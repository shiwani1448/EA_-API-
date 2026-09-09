# Candidate Call Module - API Documentation for Angular

**Base URL:** `http://localhost:5000/api/CandidateCalls`

**Response Wrapper:** All responses follow this structure:
```typescript
{
  success: boolean;
  message: string;
  
  data: T | null;
  errors?: string[];
}
```

---

## 1. Create Call Record

**Endpoint:** `POST /api/CandidateCalls`

**Purpose:** Create a new call entry when a recruiter contacts a candidate.

**Request Body:**
```typescript
{
  candidateId: number;           // Required: ID of the candidate
  requisitionId: number;          // Required: Hiring request ID
  recruiterId: number;            // Required: ID of the recruiter making the call
  callType: string;               // Required: Type of call (e.g., "Outbound", "Inbound")
  callStatus: string;             // Required: Status of the call
  candidateResponse: string;      // Required: Candidate's response
  discussionSummary?: string;     // Optional: Summary of discussion (max 1000 chars)
  remarks?: string;               // Optional: Additional remarks (max 1000 chars)
  nextFollowUpDate?: datetime;    // Optional: Date for next follow-up
  interviewScheduled: boolean;    // Required: Whether interview is scheduled
  interviewDate?: datetime;       // Optional: Date/time of interview (required if interviewScheduled=true)
  createdBy: number;              // Required: User ID creating the record
}
```

**Response:**
```typescript
{
  success: true,
  message: "Call record created successfully",
  data: {
    CallId: number
  }
}
```

**HTTP Status Codes:** 201 Created | 400 Bad Request

**Example Request:**
```json
{
  "candidateId": 4,
  "requisitionId": 1,
  "recruiterId": 101,
  "callType": "Outbound",
  "callStatus": "Connected",
  "candidateResponse": "Interested",
  "discussionSummary": "Candidate is interested in the position.",
  "remarks": "Schedule technical interview.",
  "nextFollowUpDate": "2026-06-15T10:00:00",
  "interviewScheduled": false,
  "createdBy": 101
}
```

---

## 2. Get All Calls (with Pagination & Filtering)

**Endpoint:** `GET /api/CandidateCalls`

**Purpose:** Retrieve paginated list of call records with optional filtering.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| pageNumber | int | No | Page number (default: 1) |
| pageSize | int | No | Records per page (default: 20) |
| candidateId | int | No | Filter by candidate ID |
| requisitionId | int | No | Filter by requisition ID |
| recruiterId | int | No | Filter by recruiter ID |
| callStatus | string | No | Filter by call status |
| fromDate | datetime | No | Filter calls from this date |
| toDate | datetime | No | Filter calls until this date |

**Response:**
```typescript
{
  success: true,
  message: "Call records fetched successfully.",
  data: {
    items: [
      {
        callId: number;
        candidateId: number;
        requisitionId: number;
        recruiterId: number;
        callType: string;
        callStatus: string;
        candidateResponse: string;
        discussionSummary?: string;
        remarks?: string;
        nextFollowUpDate?: datetime;
        interviewScheduled: boolean;
        interviewDate?: datetime;
        createdBy: number;
        callDateTime: datetime;
        createdDate: datetime;
        modifiedBy?: number;
        modifiedDate?: datetime;
      }
    ],
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  }
}
```

**HTTP Status Code:** 200 OK

**Example Request:**
```
GET /api/CandidateCalls?pageNumber=1&pageSize=20&callStatus=Connected
```

---

## 3. Get Call by ID

**Endpoint:** `GET /api/CandidateCalls/{callId}`

**Purpose:** Retrieve complete details of a specific call record.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| callId | int | ID of the call record |

**Response:**
```typescript
{
  success: true,
  message: "Call record fetched successfully.",
  data: {
    callId: number;
    candidateId: number;
    requisitionId: number;
    recruiterId: number;
    callType: string;
    callStatus: string;
    candidateResponse: string;
    discussionSummary?: string;
    remarks?: string;
    nextFollowUpDate?: datetime;
    interviewScheduled: boolean;
    interviewDate?: datetime;
    createdBy: number;
    callDateTime: datetime;
    createdDate: datetime;
    modifiedBy?: number;
    modifiedDate?: datetime;
  }
}
```

**HTTP Status Codes:** 200 OK | 404 Not Found

**Example Request:**
```
GET /api/CandidateCalls/5
```

---

## 4. Update Call Record

**Endpoint:** `PUT /api/CandidateCalls/{callId}`

**Purpose:** Update call outcome, follow-up date, interview details, and remarks.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| callId | int | ID of the call record |

**Request Body:**
```typescript
{
  callStatus: string;             // Required: Updated call status
  candidateResponse: string;      // Required: Updated candidate response
  discussionSummary?: string;     // Optional: Updated summary (max 1000 chars)
  remarks?: string;               // Optional: Updated remarks (max 1000 chars)
  nextFollowUpDate?: datetime;    // Optional: New follow-up date
  interviewScheduled: boolean;    // Required: Updated interview flag
  interviewDate?: datetime;       // Optional: Updated interview date (required if interviewScheduled=true)
  modifiedBy: number;             // Required: User ID performing the update
}
```

**Response:** (Same structure as Get Call by ID)

**HTTP Status Codes:** 200 OK | 400 Bad Request | 404 Not Found

**Example Request:**
```
PUT /api/CandidateCalls/5
```

**Example Body:**
```json
{
  "callStatus": "Connected",
  "candidateResponse": "Interested",
  "discussionSummary": "Candidate agreed for interview.",
  "remarks": "Interview scheduled.",
  "nextFollowUpDate": "2026-06-20T10:00:00",
  "interviewScheduled": true,
  "interviewDate": "2026-06-25T11:00:00",
  "modifiedBy": 101
}
```

---

## 5. Delete Call Record

**Endpoint:** `DELETE /api/CandidateCalls/{callId}`

**Purpose:** Delete a call record (soft delete - marks as IsDeleted=true).

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| callId | int | ID of the call record |

**Response:**
```typescript
{
  success: true,
  message: "Call record deleted successfully.",
  data: null
}
```

**HTTP Status Codes:** 200 OK | 404 Not Found

**Example Request:**
```
DELETE /api/CandidateCalls/5
```

---

## 6. Get Candidate Call History

**Endpoint:** `GET /api/CandidateCalls/candidate/{candidateId}`

**Purpose:** Show complete calling history for a candidate.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| candidateId | int | ID of the candidate |

**Response:**
```typescript
{
  success: true,
  message: "Candidate call history fetched successfully.",
  data: [
    {
      callId: number;
      callDateTime: datetime;
      callStatus: string;
      candidateResponse: string;
    }
  ]
}
```

**HTTP Status Code:** 200 OK

**Example Request:**
```
GET /api/CandidateCalls/candidate/4
```

---

## 7. Get Calls by Requisition

**Endpoint:** `GET /api/CandidateCalls/requisition/{requisitionId}`

**Purpose:** Show all candidate calls against a specific job requisition.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| requisitionId | int | ID of the requisition |

**Response:**
```typescript
{
  success: true,
  message: "Requisition call list fetched successfully.",
  data: [
    {
      callId: number;
      candidateId: number;
      requisitionId: number;
      recruiterId: number;
      callType: string;
      callStatus: string;
      candidateResponse: string;
      discussionSummary?: string;
      remarks?: string;
      nextFollowUpDate?: datetime;
      interviewScheduled: boolean;
      interviewDate?: datetime;
      createdBy: number;
      callDateTime: datetime;
      createdDate: datetime;
      modifiedBy?: number;
      modifiedDate?: datetime;
    }
  ]
}
```

**HTTP Status Code:** 200 OK

**Example Request:**
```
GET /api/CandidateCalls/requisition/1
```

---

## 8. Get Pending Follow-Ups

**Endpoint:** `GET /api/CandidateCalls/pending-followups`

**Purpose:** Return all calls where CallStatus = "Pending" OR NextFollowUpDate is set.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| recruiterId | int | No | Filter pending follow-ups for a specific recruiter |

**Response:**
```typescript
{
  success: true,
  message: "Pending follow-ups fetched successfully.",
  data: [
    {
      callId: number;
      candidateId: number;
      requisitionId: number;
      recruiterId: number;
      callType: string;
      callStatus: string;
      candidateResponse: string;
      discussionSummary?: string;
      remarks?: string;
      nextFollowUpDate?: datetime;
      interviewScheduled: boolean;
      interviewDate?: datetime;
      createdBy: number;
      callDateTime: datetime;
      createdDate: datetime;
      modifiedBy?: number;
      modifiedDate?: datetime;
    }
  ]
}
```

**HTTP Status Code:** 200 OK

**Example Requests:**
```
GET /api/CandidateCalls/pending-followups
GET /api/CandidateCalls/pending-followups?recruiterId=101
```

---

## 9. Schedule Follow-Up

**Endpoint:** `POST /api/CandidateCalls/{callId}/followup`

**Purpose:** Create or update follow-up for a candidate call.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| callId | int | ID of the call record |

**Request Body:**
```typescript
{
  nextFollowUpDate: datetime;     // Required: Date for next follow-up
  remarks?: string;               // Optional: Follow-up remarks (max 1000 chars)
}
```

**Response:** (Same structure as Get Call by ID)

**HTTP Status Codes:** 200 OK | 400 Bad Request | 404 Not Found

**Example Request:**
```
POST /api/CandidateCalls/5/followup
```

**Example Body:**
```json
{
  "nextFollowUpDate": "2026-06-15T11:00:00",
  "remarks": "Call after salary discussion."
}
```

---

## 10. Schedule Interview

**Endpoint:** `POST /api/CandidateCalls/{callId}/schedule-interview`

**Purpose:** Mark candidate as interview scheduled directly from calling module.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| callId | int | ID of the call record |

**Request Body:**
```typescript
{
  interviewDate: datetime;        // Required: Date/time of interview
  remarks?: string;               // Optional: Interview remarks (max 1000 chars)
}
```

**Response:** (Same structure as Get Call by ID)

**HTTP Status Codes:** 200 OK | 400 Bad Request | 404 Not Found

**Example Request:**
```
POST /api/CandidateCalls/5/schedule-interview
```

**Example Body:**
```json
{
  "interviewDate": "2026-06-25T11:00:00",
  "remarks": "Technical Round"
}
```

---

## Validation Rules

### Call Status Values (Valid Options)
```
Pending
Connected
Not Answered
Busy
Switched Off
Invalid Number
Wrong Number
Rejected Call
```

### Candidate Response Values (Valid Options)
```
Interested
Not Interested
Call Back Later
Already Employed
Joined Elsewhere
Salary Issue
Location Issue
Interview Scheduled
```

---

## Error Responses

**400 Bad Request Example:**
```json
{
  "success": false,
  "message": "Invalid CallStatus. Allowed values: Pending, Connected, ...",
  "errors": ["Invalid CallStatus value"]
}
```

**404 Not Found Example:**
```json
{
  "success": false,
  "message": "Call record not found.",
  "errors": null
}
```

---

## Angular Service Template

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class CandidateCallService {
  private baseUrl = 'http://localhost:5000/api/CandidateCalls';

  constructor(private http: HttpClient) {}

  // Create a new call
  createCall(dto: any): Observable<any> {
    return this.http.post(`${this.baseUrl}`, dto);
  }

  // Get all calls with pagination & filtering
  getAllCalls(pageNumber = 1, pageSize = 20, filters?: any): Observable<any> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    
    if (filters) {
      if (filters.candidateId) params = params.set('candidateId', filters.candidateId);
      if (filters.requisitionId) params = params.set('requisitionId', filters.requisitionId);
      if (filters.recruiterId) params = params.set('recruiterId', filters.recruiterId);
      if (filters.callStatus) params = params.set('callStatus', filters.callStatus);
      if (filters.fromDate) params = params.set('fromDate', filters.fromDate);
      if (filters.toDate) params = params.set('toDate', filters.toDate);
    }
    
    return this.http.get(`${this.baseUrl}`, { params });
  }

  // Get call by ID
  getCallById(callId: number): Observable<any> {
    return this.http.get(`${this.baseUrl}/${callId}`);
  }

  // Update call
  updateCall(callId: number, dto: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/${callId}`, dto);
  }

  // Delete call
  deleteCall(callId: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/${callId}`);
  }

  // Get candidate call history
  getCandidateHistory(candidateId: number): Observable<any> {
    return this.http.get(`${this.baseUrl}/candidate/${candidateId}`);
  }

  // Get calls by requisition
  getCallsByRequisition(requisitionId: number): Observable<any> {
    return this.http.get(`${this.baseUrl}/requisition/${requisitionId}`);
  }

  // Get pending follow-ups
  getPendingFollowUps(recruiterId?: number): Observable<any> {
    let params = new HttpParams();
    if (recruiterId) params = params.set('recruiterId', recruiterId);
    return this.http.get(`${this.baseUrl}/pending-followups`, { params });
  }

  // Schedule follow-up
  scheduleFollowUp(callId: number, dto: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/${callId}/followup`, dto);
  }

  // Schedule interview
  scheduleInterview(callId: number, dto: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/${callId}/schedule-interview`, dto);
  }
}
```

---

## Notes for Angular Integration

1. **Response Wrapper:** Always check `response.success` before accessing `response.data`
2. **Datetime Format:** Use ISO 8601 format: `YYYY-MM-DDTHH:mm:ss`
3. **Pagination:** Default page size is 20. Adjust `pageSize` query parameter as needed.
4. **Soft Delete:** Deleted records are marked with `IsDeleted=true` and won't appear in list queries.
5. **Error Handling:** Check `response.errors` array for detailed validation error messages.
6. **Authentication:** Include JWT token in Authorization header if API is secured.
