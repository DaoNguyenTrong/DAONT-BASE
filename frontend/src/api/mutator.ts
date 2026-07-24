import type { AxiosRequestConfig } from 'axios'
import { apiClient } from './client'

export const apiRequest = async <T>(config: AxiosRequestConfig): Promise<T> => {
  const response = await apiClient.request<T>(config)
  return response.data
}

export default apiRequest
