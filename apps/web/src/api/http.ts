import type { ProblemDetails } from './contracts'
import { apiBaseUrl } from '../config/env'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(
    status: number,
    problem: ProblemDetails,
  ) {
    super(problem.detail || problem.title || `Request failed with status ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export interface ApiRequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
  accessToken?: string
}

function buildUrl(path: string) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  return `${apiBaseUrl}${normalizedPath}`
}

export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}) {
  const { accessToken, body: requestBody, ...requestInit } = options
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  let body: BodyInit | undefined
  if (requestBody !== undefined) {
    headers.set('Content-Type', 'application/json')
    body = JSON.stringify(requestBody)
  }

  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  const response = await fetch(buildUrl(path), {
    ...requestInit,
    body,
    headers,
  })

  if (!response.ok) {
    let problem: ProblemDetails = { status: response.status }
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      problem.title = 'Không thể đọc phản hồi từ máy chủ.'
    }
    throw new ApiError(response.status, problem)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
