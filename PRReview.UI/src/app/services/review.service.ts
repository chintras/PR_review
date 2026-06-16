import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ReviewRequest, ReviewResponse } from '../models/review.model';

@Injectable({ providedIn: 'root' })
export class ReviewService {
  private readonly apiBase = 'https://localhost:7015';

  constructor(private http: HttpClient) {}

  submitReview(request: ReviewRequest): Observable<ReviewResponse> {
    return this.http
      .post<ReviewResponse>(`${this.apiBase}/api/review`, request)
      .pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let message = 'An unexpected error occurred.';
    if (error.status === 0) {
      message = 'Cannot reach the API server. Make sure PRReview.Api is running on port 5034.';
    } else if (error.error?.detail) {
      message = error.error.detail;
    } else if (error.error?.title) {
      message = error.error.title;
    } else if (error.message) {
      message = error.message;
    }
    return throwError(() => new Error(message));
  }
}
