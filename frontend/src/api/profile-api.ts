import { apiClient } from './client'
import type { ChangePasswordRequest, ProfileDto, ProfileUpdateRequest } from './types'

const getProfile = async (): Promise<ProfileDto> => {
  const response = await apiClient.get<ProfileDto>('/api/profile')
  return response.data
}

const updateProfile = async (data: ProfileUpdateRequest): Promise<ProfileDto> => {
  const response = await apiClient.put<ProfileDto>('/api/profile', data)
  return response.data
}

const changePassword = async (data: ChangePasswordRequest): Promise<void> => {
  await apiClient.put('/api/profile/password', data)
}

export default {
  getProfile,
  updateProfile,
  changePassword,
}
