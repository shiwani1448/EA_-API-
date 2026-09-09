# Candidate CRUD API Documentation

## Overview
The Candidate API provides comprehensive CRUD operations for managing job candidates in the HRMS (Human Resource Management System). All candidates are linked to specific job requisitions and include resume management capabilities.

---

## API Base URL
```
http://localhost:5000/api/candidates
```

---

## Authentication
All endpoints require **JWT Bearer Token** authentication. Include the token in the request header:
```
Authorization: Bearer <your_jwt_token>
```

---

## Response Format

### Success Response
All successful responses follow this structure:
```json
{
  "success": true,
  "data": {},
  "message": "Operation successful"
}
```

### Error Response
```json
{
  "success": false,
  "data": null,
  "message": "Error description"
}
```

---

## 1. GET ALL CANDIDATES

### Endpoint
```
GET /api/candidates
```

### Description
Retrieves all active candidates (not deleted), ordered by creation date (newest first).

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Response
- **Status Code:** 200 OK
- **Response Body:**

```json
{
  "success": true,
  "data": [
    {
      "candidateId": 1,
      "requisitionId": 5,
      "fullName": "John Doe",
      "email": "john.doe@example.com",
      "phoneNumber": "+1-234-567-8901",
      "yearsOfExperience": 5,
      "noticePeriod": "30 days",
      "currentCtcLpa": 12.5,
      "expectedCtcLpa": 15.0,
      "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
      "resume": "base64_encoded_file_content...",
      "resumeFileName": "john_doe_resume.pdf",
      "resumeContentType": "application/pdf",
      "createdAt": "2026-06-09T08:30:00Z",
      "updatedAt": null
    },
    {
      "candidateId": 2,
      "requisitionId": 5,
      "fullName": "Jane Smith",
      "email": "jane.smith@example.com",
      "phoneNumber": "+1-234-567-8902",
      "yearsOfExperience": 3,
      "noticePeriod": "15 days",
      "currentCtcLpa": 10.0,
      "expectedCtcLpa": 12.5,
      "keySkills": "JavaScript, React, Angular, TypeScript",
      "resume": "base64_encoded_file_content...",
      "resumeFileName": "jane_smith_resume.pdf",
      "resumeContentType": "application/pdf",
      "createdAt": "2026-06-08T14:20:00Z",
      "updatedAt": null
    }
  ],
  "message": "Candidates fetched successfully."
}
```

### cURL Example
```bash
curl -X GET "http://localhost:5000/api/candidates" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json"
```

---

## 2. GET CANDIDATE BY ID

### Endpoint
```
GET /api/candidates/{id}
```

### Description
Retrieves a specific candidate by their ID.

### Path Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| id | integer | Unique candidate identifier (required) |

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Response
- **Status Code:** 200 OK (if found)
- **Status Code:** 404 Not Found (if candidate doesn't exist)

### Success Response (200)
```json
{
  "success": true,
  "data": {
    "candidateId": 1,
    "requisitionId": 5,
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1-234-567-8901",
    "yearsOfExperience": 5,
    "noticePeriod": "30 days",
    "currentCtcLpa": 12.5,
    "expectedCtcLpa": 15.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
    "resume": "base64_encoded_file_content...",
    "resumeFileName": "john_doe_resume.pdf",
    "resumeContentType": "application/pdf",
    "createdAt": "2026-06-09T08:30:00Z",
    "updatedAt": null
  },
  "message": "Candidate fetched successfully."
}
```

### Error Response (404)
```json
{
  "success": false,
  "data": null,
  "message": "Candidate not found."
}
```

### cURL Example
```bash
curl -X GET "http://localhost:5000/api/candidates/1" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json"
```

---

## 3. GET CANDIDATES BY REQUISITION

### Endpoint
```
GET /api/candidates/by-requisition/{requisitionId}
```

### Description
Retrieves all active candidates for a specific job requisition/opening.

### Path Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| requisitionId | integer | Job requisition/request ID (required) |

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Response
- **Status Code:** 200 OK

### Response Body
```json
{
  "success": true,
  "data": [
    {
      "candidateId": 1,
      "requisitionId": 5,
      "fullName": "John Doe",
      "email": "john.doe@example.com",
      "phoneNumber": "+1-234-567-8901",
      "yearsOfExperience": 5,
      "noticePeriod": "30 days",
      "currentCtcLpa": 12.5,
      "expectedCtcLpa": 15.0,
      "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
      "resume": "base64_encoded_file_content...",
      "resumeFileName": "john_doe_resume.pdf",
      "resumeContentType": "application/pdf",
      "createdAt": "2026-06-09T08:30:00Z",
      "updatedAt": null
    }
  ],
  "message": "Candidates fetched successfully."
}
```

### cURL Example
```bash
curl -X GET "http://localhost:5000/api/candidates/by-requisition/5" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json"
```

---

## 4. CREATE CANDIDATE

### Endpoint
```
POST /api/candidates
```

### Description
Creates a new candidate record. Resume must be provided as Base64-encoded string.

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Request Body
```json
{
  "requisitionId": 5,
  "fullName": "John Doe",
  "email": "john.doe@example.com",
  "phoneNumber": "+1-234-567-8901",
  "yearsOfExperience": 5,
  "noticePeriod": "30 days",
  "currentCtcLpa": 12.5,
  "expectedCtcLpa": 15.0,
  "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
  "resume": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...",
  "resumeFileName": "john_doe_resume.pdf",
  "resumeContentType": "application/pdf"
}
```

### Request Body Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| requisitionId | integer | Yes | ID of the job requisition |
| fullName | string | No | Candidate's full name (max 255 chars) |
| email | string | No | Email address (max 255 chars) |
| phoneNumber | string | No | Contact phone number |
| yearsOfExperience | integer | No | Years of professional experience (0-99) |
| noticePeriod | string | No | Notice period (e.g., "30 days", "Immediate") |
| currentCtcLpa | decimal | No | Current CTC in LPA (Indian rupees) |
| expectedCtcLpa | decimal | No | Expected CTC in LPA |
| keySkills | string | No | Comma-separated key skills |
| resume | string | No | Base64-encoded resume file (PDF/DOC recommended) |
| resumeFileName | string | No | Original filename of resume |
| resumeContentType | string | No | MIME type (e.g., "application/pdf", "application/msword") |

### Response Codes
- **201 Created** - Candidate successfully created
- **400 Bad Request** - Invalid request data
- **404 Not Found** - Requisition doesn't exist

### Success Response (201)
```json
{
  "success": true,
  "data": {
    "candidateId": 1,
    "requisitionId": 5,
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1-234-567-8901",
    "yearsOfExperience": 5,
    "noticePeriod": "30 days",
    "currentCtcLpa": 12.5,
    "expectedCtcLpa": 15.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
    "resume": "base64_encoded_file_content...",
    "resumeFileName": "john_doe_resume.pdf",
    "resumeContentType": "application/pdf",
    "createdAt": "2026-06-09T08:30:00Z",
    "updatedAt": null
  },
  "message": "Candidate created successfully."
}
```

### Error Response - Bad Base64 (400)
```json
{
  "success": false,
  "data": null,
  "message": "Resume must be valid Base64."
}
```

### Error Response - Requisition Not Found (404)
```json
{
  "success": false,
  "data": null,
  "message": "Requisition not found."
}
```

### cURL Example
```bash
curl -X POST "http://localhost:5000/api/candidates" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json" \
  -d '{
    "requisitionId": 5,
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1-234-567-8901",
    "yearsOfExperience": 5,
    "noticePeriod": "30 days",
    "currentCtcLpa": 12.5,
    "expectedCtcLpa": 15.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
    "resume": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...",
    "resumeFileName": "john_doe_resume.pdf",
    "resumeContentType": "application/pdf"
  }'
```

---

## 5. UPDATE CANDIDATE

### Endpoint
```
PUT /api/candidates/{id}
```

### Description
Updates an existing candidate record. All fields are replaceable, including resume.

### Path Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| id | integer | Candidate ID to update (required) |

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Request Body
Same as Create Candidate endpoint.

```json
{
  "requisitionId": 5,
  "fullName": "John Doe Updated",
  "email": "john.doe.updated@example.com",
  "phoneNumber": "+1-234-567-8902",
  "yearsOfExperience": 6,
  "noticePeriod": "15 days",
  "currentCtcLpa": 13.0,
  "expectedCtcLpa": 16.0,
  "keySkills": "C#, .NET, ASP.NET Core, SQL Server, Azure",
  "resume": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...",
  "resumeFileName": "john_doe_resume_updated.pdf",
  "resumeContentType": "application/pdf"
}
```

### Response Codes
- **200 OK** - Candidate successfully updated
- **400 Bad Request** - Invalid request data
- **404 Not Found** - Candidate or Requisition not found

### Success Response (200)
```json
{
  "success": true,
  "data": {
    "candidateId": 1,
    "requisitionId": 5,
    "fullName": "John Doe Updated",
    "email": "john.doe.updated@example.com",
    "phoneNumber": "+1-234-567-8902",
    "yearsOfExperience": 6,
    "noticePeriod": "15 days",
    "currentCtcLpa": 13.0,
    "expectedCtcLpa": 16.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server, Azure",
    "resume": "base64_encoded_file_content...",
    "resumeFileName": "john_doe_resume_updated.pdf",
    "resumeContentType": "application/pdf",
    "createdAt": "2026-06-09T08:30:00Z",
    "updatedAt": "2026-06-09T10:15:00Z"
  },
  "message": "Candidate updated successfully."
}
```

### Error Response (404)
```json
{
  "success": false,
  "data": null,
  "message": "Candidate not found."
}
```

### cURL Example
```bash
curl -X PUT "http://localhost:5000/api/candidates/1" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json" \
  -d '{
    "requisitionId": 5,
    "fullName": "John Doe Updated",
    "email": "john.doe.updated@example.com",
    "phoneNumber": "+1-234-567-8902",
    "yearsOfExperience": 6,
    "noticePeriod": "15 days",
    "currentCtcLpa": 13.0,
    "expectedCtcLpa": 16.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server, Azure",
    "resume": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...",
    "resumeFileName": "john_doe_resume_updated.pdf",
    "resumeContentType": "application/pdf"
  }'
```

---

## 6. UPLOAD/UPDATE RESUME

### Endpoint
```
POST /api/candidates/{id}/resume
```

### Description
Updates only the resume for an existing candidate without modifying other fields. Useful for resume updates without affecting other candidate data.

### Path Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| id | integer | Candidate ID (required) |

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Request Body
```json
{
  "resume": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...",
  "resumeFileName": "john_doe_resume_v2.pdf",
  "resumeContentType": "application/pdf"
}
```

### Request Body Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| resume | string | Yes | Base64-encoded resume file |
| resumeFileName | string | No | Original filename of resume |
| resumeContentType | string | No | MIME type (e.g., "application/pdf") |

### Response Codes
- **200 OK** - Resume successfully uploaded
- **400 Bad Request** - Invalid Base64 encoding
- **404 Not Found** - Candidate not found

### Success Response (200)
```json
{
  "success": true,
  "data": {
    "candidateId": 1,
    "requisitionId": 5,
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1-234-567-8901",
    "yearsOfExperience": 5,
    "noticePeriod": "30 days",
    "currentCtcLpa": 12.5,
    "expectedCtcLpa": 15.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
    "resume": "base64_encoded_file_content...",
    "resumeFileName": "john_doe_resume_v2.pdf",
    "resumeContentType": "application/pdf",
    "createdAt": "2026-06-09T08:30:00Z",
    "updatedAt": "2026-06-09T11:00:00Z"
  },
  "message": "Resume uploaded successfully."
}
```

### Error Response (400)
```json
{
  "success": false,
  "data": null,
  "message": "Resume must be valid Base64."
}
```

### cURL Example
```bash
curl -X POST "http://localhost:5000/api/candidates/1/resume" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json" \
  -d '{
    "resume": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...",
    "resumeFileName": "john_doe_resume_v2.pdf",
    "resumeContentType": "application/pdf"
  }'
```

---

## 7. DELETE CANDIDATE

### Endpoint
```
DELETE /api/candidates/{id}
```

### Description
Soft deletes a candidate (marks as deleted, doesn't remove from database).

### Path Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| id | integer | Candidate ID to delete (required) |

### Headers
```
Authorization: Bearer <token>
Content-Type: application/json
```

### Response Codes
- **200 OK** - Candidate successfully deleted
- **404 Not Found** - Candidate not found

### Success Response (200)
```json
{
  "success": true,
  "data": {
    "candidateId": 1,
    "requisitionId": 5,
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1-234-567-8901",
    "yearsOfExperience": 5,
    "noticePeriod": "30 days",
    "currentCtcLpa": 12.5,
    "expectedCtcLpa": 15.0,
    "keySkills": "C#, .NET, ASP.NET Core, SQL Server",
    "resume": "base64_encoded_file_content...",
    "resumeFileName": "john_doe_resume.pdf",
    "resumeContentType": "application/pdf",
    "createdAt": "2026-06-09T08:30:00Z",
    "updatedAt": "2026-06-09T11:05:00Z"
  },
  "message": "Candidate deleted successfully."
}
```

### Error Response (404)
```json
{
  "success": false,
  "data": null,
  "message": "Candidate not found."
}
```

### cURL Example
```bash
curl -X DELETE "http://localhost:5000/api/candidates/1" \
  -H "Authorization: Bearer your_jwt_token" \
  -H "Content-Type: application/json"
```

---

## Data Models

### Candidate Entity
| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| CandidateId | int | No | Primary key (auto-generated) |
| RequisitionId | int | No | Foreign key to hiring request |
| FullName | string | Yes | Candidate's full name |
| Email | string | Yes | Email address |
| PhoneNumber | string | Yes | Contact number |
| YearsOfExperience | int | Yes | Years of experience |
| NoticePeriod | string | Yes | Notice period (e.g., "30 days") |
| CurrentCtcLpa | decimal | Yes | Current salary in LPA |
| ExpectedCtcLpa | decimal | Yes | Expected salary in LPA |
| KeySkills | string | Yes | Comma-separated skills |
| Resume | byte[] | Yes | Binary resume content |
| ResumeFileName | string | Yes | Original resume filename |
| ResumeContentType | string | Yes | MIME type of resume |
| CreatedAt | DateTime | No | UTC timestamp of creation |
| UpdatedAt | DateTime | Yes | UTC timestamp of last update |
| IsDeleted | bool | No | Soft delete flag (default: false) |

---

## Resume Handling

### Base64 Encoding Format
The API accepts resumes in two formats:
1. **Raw Base64:** `JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...`
2. **Data URI Format:** `data:application/pdf;base64,JVBERi0xLjQKJeLjz9MNCjEgMCBvYmo...`

Both formats are automatically parsed and converted to binary data.

### Supported File Types
- **PDF:** `application/pdf`
- **Word:** `application/msword` (DOC), `application/vnd.openxmlformats-officedocument.wordprocessingml.document` (DOCX)
- **Text:** `text/plain`

### Encoding in Angular
```typescript
// Convert file to Base64
convertFileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

// Usage in form submission
async onFileSelected(event: any) {
  const file = event.target.files[0];
  const base64Resume = await this.convertFileToBase64(file);
  
  this.candidateForm.patchValue({
    resume: base64Resume,
    resumeFileName: file.name,
    resumeContentType: file.type
  });
}
```

---

## Validation Rules

### Required Fields
- **RequisitionId:** Must exist in the HiringRequests table

### Optional Field Validation
- **Email:** Should be in valid email format
- **PhoneNumber:** Should be in valid phone format
- **YearsOfExperience:** Should be 0-99
- **CurrentCtcLpa & ExpectedCtcLpa:** Positive decimal values
- **Resume:** Must be valid Base64 encoding

---

## Status Codes Summary

| Code | Meaning | Usage |
|------|---------|-------|
| 200 | OK | GET, PUT, POST (resume upload), DELETE successful |
| 201 | Created | POST (create candidate) successful |
| 400 | Bad Request | Invalid data, invalid Base64 resume |
| 404 | Not Found | Candidate or Requisition not found |
| 401 | Unauthorized | Missing or invalid JWT token |
| 500 | Internal Server Error | Server error |

---

## Error Handling Guide

### Common Error Scenarios

**1. Missing JWT Token**
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

**2. Invalid Base64 Resume**
```json
{
  "success": false,
  "data": null,
  "message": "Resume must be valid Base64."
}
```

**3. Requisition Not Found**
```json
{
  "success": false,
  "data": null,
  "message": "Requisition not found."
}
```

**4. Candidate Not Found**
```json
{
  "success": false,
  "data": null,
  "message": "Candidate not found."
}
```

---

## Angular Integration Example

### Service Implementation
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class CandidateService {
  private apiUrl = 'http://localhost:5000/api/candidates';

  constructor(private http: HttpClient) {}

  // Get all candidates
  getAllCandidates(): Observable<any> {
    return this.http.get<any>(this.apiUrl);
  }

  // Get candidate by ID
  getCandidateById(id: number): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}`);
  }

  // Get candidates by requisition
  getCandidatesByRequisition(requisitionId: number): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/by-requisition/${requisitionId}`);
  }

  // Create candidate
  createCandidate(candidate: any): Observable<any> {
    return this.http.post<any>(this.apiUrl, candidate);
  }

  // Update candidate
  updateCandidate(id: number, candidate: any): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/${id}`, candidate);
  }

  // Upload resume
  uploadResume(id: number, resume: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/${id}/resume`, resume);
  }

  // Delete candidate
  deleteCandidate(id: number): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/${id}`);
  }
}
```

### Component Usage
```typescript
import { Component, OnInit } from '@angular/core';
import { CandidateService } from './candidate.service';

@Component({
  selector: 'app-candidate',
  templateUrl: './candidate.component.html',
  styleUrls: ['./candidate.component.css']
})
export class CandidateComponent implements OnInit {
  candidates: any[] = [];
  candidateForm: any = {};
  selectedRequisitionId: number = 0;

  constructor(private candidateService: CandidateService) {}

  ngOnInit() {
    this.loadCandidates();
  }

  loadCandidates() {
    this.candidateService.getAllCandidates().subscribe(
      (response: any) => {
        if (response.success) {
          this.candidates = response.data;
        }
      },
      (error: any) => console.error('Error loading candidates', error)
    );
  }

  createCandidate() {
    this.candidateService.createCandidate(this.candidateForm).subscribe(
      (response: any) => {
        if (response.success) {
          alert('Candidate created successfully');
          this.loadCandidates();
        }
      },
      (error: any) => alert('Error creating candidate')
    );
  }

  updateCandidate(id: number) {
    this.candidateService.updateCandidate(id, this.candidateForm).subscribe(
      (response: any) => {
        if (response.success) {
          alert('Candidate updated successfully');
          this.loadCandidates();
        }
      },
      (error: any) => alert('Error updating candidate')
    );
  }

  deleteCandidate(id: number) {
    if (confirm('Are you sure you want to delete this candidate?')) {
      this.candidateService.deleteCandidate(id).subscribe(
        (response: any) => {
          if (response.success) {
            alert('Candidate deleted successfully');
            this.loadCandidates();
          }
        },
        (error: any) => alert('Error deleting candidate')
      );
    }
  }
}
```

---

## Rate Limiting & Performance Notes

- No rate limiting is currently implemented
- Resume files are stored in the database; consider setting a maximum file size (recommended: 5-10 MB)
- For bulk operations, implement pagination in future versions
- Consider caching GET requests for better performance

---

## CORS Configuration

The API is configured to accept requests from Angular running on:
```
http://localhost:4200
```

If running on a different port, update the CORS configuration in `Program.cs`.

---

## Testing with Postman

1. **Authenticate:**
   - POST to your auth endpoint to get JWT token
   - Copy the token

2. **In Postman:**
   - Set Authorization tab → Bearer Token → Paste token
   - Use the provided endpoints

3. **Sample Request:**
   ```
   POST /api/candidates
   Authorization: Bearer <token>
   Content-Type: application/json
   
   Body: {
     "requisitionId": 1,
     "fullName": "Test User",
     "email": "test@example.com"
   }
   ```

---

## Support & Issues

For API issues or questions, contact the development team or check the application logs for detailed error information.

---

**Last Updated:** June 9, 2026
**API Version:** 1.0
**Status:** Production Ready
