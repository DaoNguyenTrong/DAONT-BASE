---
title: Manage roles
description: 'Create, edit and delete roles, and choose the permissions of each.'
permission: organizations.roles.manage
---

# Manage roles

A role is a set of permissions. You give members roles rather than individual permissions. Create your own roles when the built-in ones don't match how your team works.

## System roles

Every organization comes with three roles tagged **System**. They cannot be edited or deleted.

| Role       | Permissions                                           |
| ---------- | ----------------------------------------------------- |
| **Owner**  | The organization's owner, always has every permission |
| **Admin**  | **Manage members**                                    |
| **Member** | No management permissions; works in the organization  |

## Permissions

| Permission              | Allows                                                                                 |
| ----------------------- | -------------------------------------------------------------------------------------- |
| **Manage organization** | [Deactivating the organization](./deactivate)                                          |
| **Manage members**      | [Adding](./add-member), changing the roles of and [removing members](./manage-members) |
| **Manage roles**        | Creating, editing and deleting roles (this page)                                       |

::: warning
**Manage organization** allows deactivating the organization, which cannot be undone. Give it only to people who really need it.
:::

**Manage files**, **Manage API keys**, **View audit logs** and **Manage settings** only take effect when your system has the matching features.

## View roles

1. Click **Organizations** in the sidebar.
2. On the organization's row, click the **Roles** button (the gear icon).

<figure>

![The organization's roles dialog](/base/images/en/organizations/roles.jpg)

<figcaption>The roles dialog</figcaption>
</figure>

| #   | Element                                                    |
| --- | ---------------------------------------------------------- |
| 1   | The **System** tag: a role that can't be edited or deleted |
| 2   | The **Edit role** button                                   |
| 3   | The delete-role button                                     |
| 4   | The **Create role** button                                 |

## Create a role

1. In the roles dialog, click **Create role**.
2. Enter the role's **Name**.
3. Under **Permissions**, tick the permissions this role should have.
4. Click **Save**.

<figure>

![The Create role dialog](/base/images/en/organizations/role-form.jpg)

<figcaption>The Create role dialog</figcaption>
</figure>

| #   | Element                  |
| --- | ------------------------ |
| 1   | The **Name** field       |
| 2   | The **Permissions** list |
| 3   | The **Save** button      |

The app shows **Role created**. The new role can be chosen right away when adding a member or changing roles.

## Edit or delete a role

- **Edit**: click the **Edit role** button (the pencil icon) on the role's row, change its name or permissions, then click **Save**.
- **Delete**: click the trash button on the role's row, then click **Confirm**. Members holding only this role are left with no role; [give them a role again](./manage-members).

## Troubleshooting

- **No Roles button on the organization's row**: you don't have the **Manage roles** permission in that organization.
- **A role has no edit or delete buttons**: it is a **System** role.
