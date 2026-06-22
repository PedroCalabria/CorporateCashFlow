import { http, HttpResponse } from 'msw'
import type {
  LoginResponse,
  TokenRefreshResponse,
  UserContextResponse,
} from '@/features/auth/types/auth.types'

const consumedRefreshTokens = new Set<string>()

const editorUser: UserContextResponse = {
  id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  name: 'Jane Doe',
  email: 'jane.doe@example.com',
  role: 'Editor',
  subsidiaryId: '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
}

const adminUser: UserContextResponse = {
  id: '1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d',
  name: 'Admin User',
  email: 'admin@example.com',
  role: 'Admin',
  subsidiaryId: null,
}

const mockLoginResponse: LoginResponse = {
  token: 'mock-access-token',
  refreshToken: 'mock-refresh-token',
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
}

const mockRefreshResponse: TokenRefreshResponse = {
  accessToken: 'mock-refreshed-access-token',
  refreshToken: 'mock-refreshed-refresh-token',
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
}

export const authHandlers = [
  http.post('/api/auth/login', async ({ request }) => {
    const body = (await request.json()) as { email?: string; password?: string }

    if (!body.email || !body.email.includes('@')) {
      return HttpResponse.json(
        {
          type: 'https://tools.ietf.org/html/rfc7807',
          title: 'Validation Failed',
          status: 400,
          detail: 'One or more validation errors occurred.',
          errors: { email: ['The email field is not a valid e-mail address.'] },
        },
        { status: 400 },
      )
    }

    if (body.email === 'jane.doe@example.com' && body.password === 'S3cur3P@ss') {
      consumedRefreshTokens.clear()
      return HttpResponse.json(mockLoginResponse)
    }

    return HttpResponse.json(
      {
        type: 'https://tools.ietf.org/html/rfc7807',
        title: 'Unauthorized',
        status: 401,
        detail: 'Invalid email or password.',
      },
      { status: 401 },
    )
  }),

  http.get('/api/auth/me', ({ request }) => {
    const auth = request.headers.get('Authorization')

    if (!auth || auth === 'Bearer expired-token') {
      return HttpResponse.json(
        {
          type: 'https://tools.ietf.org/html/rfc7807',
          title: 'Unauthorized',
          status: 401,
          detail: 'Authorization token is required.',
        },
        { status: 401 },
      )
    }

    if (auth.includes('admin')) {
      return HttpResponse.json(adminUser)
    }

    return HttpResponse.json(editorUser)
  }),

  http.post('/api/auth/refresh', async ({ request }) => {
    const body = (await request.json()) as { accessToken?: string; refreshToken?: string }

    if (!body.refreshToken) {
      return HttpResponse.json(
        {
          type: 'https://tools.ietf.org/html/rfc7807',
          title: 'Validation Failed',
          status: 400,
          detail: 'One or more validation errors occurred.',
          errors: { refreshToken: ['The refreshToken field is required.'] },
        },
        { status: 400 },
      )
    }

    if (
      body.refreshToken === 'mock-refresh-token' ||
      body.refreshToken === 'mock-refreshed-refresh-token'
    ) {
      if (consumedRefreshTokens.has(body.refreshToken)) {
        return HttpResponse.json(
          {
            type: 'https://tools.ietf.org/html/rfc7807',
            title: 'Unauthorized',
            status: 401,
            detail: 'The refresh token has already been used or revoked.',
          },
          { status: 401 },
        )
      }

      consumedRefreshTokens.add(body.refreshToken)
      return HttpResponse.json(mockRefreshResponse)
    }

    return HttpResponse.json(
      {
        type: 'https://tools.ietf.org/html/rfc7807',
        title: 'Unauthorized',
        status: 401,
        detail: 'The refresh token has expired.',
      },
      { status: 401 },
    )
  }),

  http.post('/api/auth/logout', ({ request }) => {
    const auth = request.headers.get('Authorization')

    if (!auth) {
      return HttpResponse.json(
        {
          type: 'https://tools.ietf.org/html/rfc7807',
          title: 'Unauthorized',
          status: 401,
          detail: 'Authorization token is required.',
        },
        { status: 401 },
      )
    }

    return new HttpResponse(null, { status: 204 })
  }),
]
