import { describe, expect, it } from 'vitest'
import { reactive } from 'vue'
import AccountForm from '@/components/AccountForm.vue'
import type { CreateAccountRequest } from '@/api/types'
import { renderComponent } from '../helpers/render'

function makeFormState(overrides: Partial<CreateAccountRequest> = {}): CreateAccountRequest {
  return {
    name: '',
    username: '',
    email: '',
    password: '',
    phone: null,
    position: null,
    address: null,
    status: true,
    ...overrides,
  }
}

async function mountAccountForm(options: { isEditing: boolean; state?: CreateAccountRequest }) {
  const state = reactive(options.state ?? makeFormState())

  const { wrapper } = await renderComponent(AccountForm, {
    props: {
      state,
      isEditing: options.isEditing,
    },
  })

  return { wrapper, state }
}

describe('AccountForm', () => {
  it('shows password field in create mode', async () => {
    const { wrapper } = await mountAccountForm({ isEditing: false })

    expect(wrapper.text()).toContain('Password')
  })

  it('hides password field in edit mode', async () => {
    const { wrapper } = await mountAccountForm({ isEditing: true })

    expect(wrapper.text()).not.toContain('Password')
  })

  it('binds name input to dialog state', async () => {
    const { wrapper, state } = await mountAccountForm({ isEditing: false })

    const nameInput = wrapper.find('input')
    await nameInput.setValue('Alice Nguyen')

    expect(state.name).toBe('Alice Nguyen')
  })
})
