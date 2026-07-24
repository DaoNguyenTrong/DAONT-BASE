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

  it('binds username, email, phone, position, and address inputs to dialog state', async () => {
    const { wrapper, state } = await mountAccountForm({ isEditing: false })
    const inputs = wrapper.findAll('input[type="text"]')

    await inputs[0].setValue('Alice Nguyen') // name
    await inputs[1].setValue('alice')
    await inputs[2].setValue('alice@example.com')
    await inputs[3].setValue('0900000000')
    await inputs[4].setValue('Engineer')
    await inputs[5].setValue('123 Main St')

    expect(state.username).toBe('alice')
    expect(state.email).toBe('alice@example.com')
    expect(state.phone).toBe('0900000000')
    expect(state.position).toBe('Engineer')
    expect(state.address).toBe('123 Main St')
  })

  it('binds the password input to dialog state in create mode', async () => {
    const { wrapper, state } = await mountAccountForm({ isEditing: false })

    await wrapper.find('input[type="password"]').setValue('S3cret!')

    expect(state.password).toBe('S3cret!')
  })

  it('binds the status checkbox to dialog state', async () => {
    const { wrapper, state } = await mountAccountForm({
      isEditing: false,
      state: {
        name: '',
        username: '',
        email: '',
        password: '',
        phone: null,
        position: null,
        address: null,
        status: true,
      },
    })

    await wrapper.get('.n-checkbox').trigger('click')

    expect(state.status).toBe(false)
  })
})
