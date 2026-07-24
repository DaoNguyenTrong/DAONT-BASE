import { apiClient } from './client'
import type { HealthResponse } from './types'

const getHealth = async (): Promise<HealthResponse> => {
  const response = await apiClient.get<HealthResponse>('/api/health')
  return response.data
}

export default {
  getHealth,
}
