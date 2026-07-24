import { apiClient } from './client'
import type {
  Account,
  AccountPagedResult,
  CreateAccountRequest,
  UpdateAccountRequest,
} from './types'

export interface AccountFilters {
  search?: string
}

const getAll = async (
  pageNumber: number,
  pageSize: number,
  filters?: AccountFilters,
): Promise<AccountPagedResult> => {
  const response = await apiClient.get<AccountPagedResult>('/api/accounts', {
    params: {
      pageNumber,
      pageSize,
      ...(filters?.search ? { search: filters.search } : {}),
    },
  })
  return response.data
}

const getById = async (id: string): Promise<Account> => {
  const response = await apiClient.get<Account>(`/api/accounts/${id}`)
  return response.data
}

const create = async (data: CreateAccountRequest): Promise<Account> => {
  const response = await apiClient.post<Account>('/api/accounts', data)
  return response.data
}

const update = async (id: string, data: UpdateAccountRequest): Promise<Account> => {
  const response = await apiClient.put<Account>(`/api/accounts/${id}`, data)
  return response.data
}

const remove = async (id: string): Promise<void> => {
  await apiClient.delete(`/api/accounts/${id}`)
}

export default {
  getAll,
  getById,
  create,
  update,
  remove,
}
