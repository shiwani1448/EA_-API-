# Candidate Stage/Status API

## Update candidate stage and status

`PATCH /api/Candidates/{candidateId}/stage-status`

Request body:

```json
{
  "currentStage": "screening",
  "currentStatus": "shorting"
}
```

Notes:
- `currentStage` is required.
- `currentStatus` is required.
- When `currentStatus` is `shorting`, `shortlisting`, or `shortlisted`, the API sets `shortlistingDate` automatically if it is not already set.
- The API returns the full candidate object.

Response example:

```json
{
  "success": true,
  "message": "Candidate stage/status updated successfully.",
  "data": {
    "candidateId": 12,
    "requisitionId": 5,
    "fullName": "Amit Sharma",
    "email": "amit@example.com",
    "phoneNumber": "9999999999",
    "yearsOfExperience": 4,
    "noticePeriod": "30 days",
    "currentCtcLpa": 8.5,
    "expectedCtcLpa": 11,
    "keySkills": "C#, .NET, SQL",
    "source": "LinkedIn",
    "resumePath": "/uploads/resumes/file.pdf",
    "resumeFileName": "file.pdf",
    "resumeContentType": "application/pdf",
    "currentStage": "screening",
    "currentStatus": "shorting",
    "shortlistingDate": "2026-06-10T10:15:00Z",
    "createdAt": "2026-06-10T09:30:00Z",
    "updatedAt": "2026-06-10T10:15:00Z"
  }
}
```

## Candidate fetch APIs

These existing APIs now include `currentStage`, `currentStatus`, and `shortlistingDate` in each candidate object:

- `GET /api/Candidates`
- `GET /api/Candidates/{candidateId}`
- `GET /api/Candidates/by-requisition/{requisitionId}`
- `POST /api/Candidates`
- `PUT /api/Candidates/{candidateId}`
- `POST /api/Candidates/{candidateId}/resume`
- `DELETE /api/Candidates/{candidateId}`
