import { getOrganizations } from '@/api/generated/organizations/organizations'
import type { CreateOrganizationRequest, OrganizationDto } from '@/api/types'

export const useOrganizationsStore = defineStore('organizations', () => {
  const client = getOrganizations()
  const myOrganizations = ref<OrganizationDto[]>([])
  const loaded = ref(false)

  async function fetchMine(): Promise<OrganizationDto[]> {
    myOrganizations.value = await client.organizationsGetMine()
    loaded.value = true
    return myOrganizations.value
  }

  async function create(data: CreateOrganizationRequest): Promise<OrganizationDto> {
    const organization = await client.organizationsCreate(data)
    myOrganizations.value = [...myOrganizations.value, organization]
    return organization
  }

  async function deactivate(id: string): Promise<void> {
    await client.organizationsDeactivate(id)
    myOrganizations.value = myOrganizations.value.filter((organization) => organization.id !== id)
  }

  return {
    myOrganizations,
    loaded,
    fetchMine,
    create,
    deactivate,
  }
})
