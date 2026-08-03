import { getOrganizations } from '@/api/generated/organizations/organizations'
import { getPermissions } from '@/api/generated/permissions/permissions'
import type { CreateRoleRequest, PermissionDto, RoleDto, UpdateRoleRequest } from '@/api/types'

export const useRolesStore = defineStore('roles', () => {
  const client = getOrganizations()
  const permissionsClient = getPermissions()
  const roles = ref<RoleDto[]>([])
  const permissionCatalog = ref<PermissionDto[]>([])

  async function fetchRoles(organizationId: string): Promise<RoleDto[]> {
    roles.value = await client.organizationsGetRoles(organizationId)
    return roles.value
  }

  async function fetchPermissionCatalog(): Promise<PermissionDto[]> {
    if (permissionCatalog.value.length === 0) {
      permissionCatalog.value = await permissionsClient.permissionsGetAll()
    }
    return permissionCatalog.value
  }

  async function create(organizationId: string, data: CreateRoleRequest): Promise<RoleDto> {
    const role = await client.organizationsCreateRole(organizationId, data)
    roles.value = [...roles.value, role]
    return role
  }

  async function update(
    organizationId: string,
    roleId: string,
    data: UpdateRoleRequest,
  ): Promise<RoleDto> {
    const role = await client.organizationsUpdateRole(organizationId, roleId, data)
    roles.value = roles.value.map((r) => (r.id === roleId ? role : r))
    return role
  }

  async function remove(organizationId: string, roleId: string): Promise<void> {
    await client.organizationsDeleteRole(organizationId, roleId)
    roles.value = roles.value.filter((r) => r.id !== roleId)
  }

  return {
    roles,
    permissionCatalog,
    fetchRoles,
    fetchPermissionCatalog,
    create,
    update,
    remove,
  }
})
