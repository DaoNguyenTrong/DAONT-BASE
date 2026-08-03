export interface LocaleSchema {
  common: {
    confirm: string
    cancel: string
    save: string
    close: string
    loading: string
    search: string
    goHome: string
    language: string
    theme: string
    lightMode: string
    darkMode: string
    fontSize: string
    decreaseFontSize: string
    increaseFontSize: string
    resetFontSize: string
    sortBy: string
    sortDefault: string
    sortAscending: string
    sortDescending: string
  }
  errors: {
    requestFailed: string
    networkError: string
    unauthorized: string
    forbidden: string
    notFound: string
    notFoundTitle: string
    notFoundDescription: string
    serverError: string
  }
  auth: {
    login: string
    logout: string
    register: string
    username: string
    password: string
    email: string
    name: string
    namePlaceholder: string
    loginTitle: string
    loginHint: string
    registerTitle: string
    loginButton: string
    registerButton: string
    keepLogin: string
    keepLoginHint: string
    usernameRequired: string
    passwordRequired: string
    invalidCredentials: string
    loginFailed: string
    logoutConfirmMessage: string
    logoutFailed: string
    usernamePlaceholder: string
    passwordPlaceholder: string
    emailNotConfirmed: string
    emailRequired: string
    emailPlaceholder: string
    resendVerification: string
    resendVerificationSuccess: string
    resendVerificationFailed: string
    nameRequired: string
    emailInvalid: string
    passwordTooShort: string
    usernameAlreadyExists: string
    emailAlreadyExists: string
    registerFailed: string
    registerSuccessTitle: string
    registerSuccessMessage: string
    backToLogin: string
    verifyingEmail: string
    verifyEmailInvalidToken: string
    verifyEmailMissingToken: string
    verifyEmailFailed: string
    orContinueWith: string
    continueWithGoogle: string
    googleEmailNotConfirmed: string
    googleEmailNotVerifiedByProvider: string
    googleLoginFailed: string
    continueWithMicrosoft: string
    microsoftEmailNotConfirmed: string
    microsoftEmailNotVerifiedByProvider: string
    microsoftLoginFailed: string
    registerLink: string
  }
  app: {
    name: string
    version: string
    copyright: string
    apiVersion: string
    apiBuildTime: string
    apiStatus: {
      checking: string
      online: string
      offline: string
    }
    welcomeTitle: string
    welcomeDescription: string
  }
  nav: {
    home: string
    organizations: string
    profile: string
    toggleSidebar: string
    collapseSidebar: string
    expandSidebar: string
  }
  profile: {
    title: string
    updateSuccess: string
    passwordChanged: string
    currentPassword: string
    newPassword: string
    changePassword: string
    personalInfo: string
    currentPasswordPlaceholder: string
    newPasswordPlaceholder: string
    name: string
    namePlaceholder: string
    email: string
    emailPlaceholder: string
    phone: string
    phonePlaceholder: string
    position: string
    positionPlaceholder: string
    address: string
    addressPlaceholder: string
    sessionsTitle: string
    sessionsEmpty: string
    currentDevice: string
    rememberLogin: string
    lastActive: string
    sessionLoginAt: string
    unknownDevice: string
    unknownIp: string
    signOutDevice: string
    signOutOtherDevices: string
    signOutDeviceConfirm: string
    signOutOtherDevicesConfirm: string
    deviceSignedOut: string
    otherDevicesSignedOut: string
  }
  organizations: {
    title: string
    personalWorkspace: string
    switchOrganization: string
    createNew: string
    createTitle: string
    manage: string
    name: string
    namePlaceholder: string
    slug: string
    slugPlaceholder: string
    slugHint: string
    myRole: string
    status: string
    active: string
    inactive: string
    empty: string
    emptyHint: string
    created: string
    switchedTo: string
    switchedToPersonal: string
    members: string
    membersTitle: string
    membersEmpty: string
    addMember: string
    addMemberTitle: string
    memberEmail: string
    memberEmailPlaceholder: string
    memberAdded: string
    removeMember: string
    removeMemberConfirm: string
    memberRemoved: string
    memberRolesUpdated: string
    deactivate: string
    deactivateConfirm: string
    deactivated: string
    roles: string
    rolesTitle: string
    rolesEmpty: string
    roleNoPermissions: string
    createRole: string
    createRoleTitle: string
    editRoleTitle: string
    roleName: string
    roleNamePlaceholder: string
    rolePermissions: string
    roleDeleteConfirm: string
    roleCreated: string
    roleUpdated: string
    roleDeleted: string
    roleSystemBadge: string
    permissionOrganizationManage: string
    permissionOrganizationMembersManage: string
    permissionOrganizationRolesManage: string
  }
}

const en: LocaleSchema = {
  common: {
    confirm: 'Confirm',
    cancel: 'Cancel',
    save: 'Save',
    close: 'Close',
    loading: 'Loading...',
    search: 'Search',
    goHome: 'Go to workspace',
    language: 'Language',
    theme: 'Theme',
    lightMode: 'Light',
    darkMode: 'Dark',
    fontSize: 'Font size',
    decreaseFontSize: 'Decrease font size',
    increaseFontSize: 'Increase font size',
    resetFontSize: 'Reset font size',
    sortBy: 'Sort by',
    sortDefault: 'Default',
    sortAscending: 'Ascending',
    sortDescending: 'Descending',
  },
  errors: {
    requestFailed: 'Request failed',
    networkError: 'Network error',
    unauthorized: 'Unauthorized',
    forbidden: 'Forbidden',
    notFound: 'Not found',
    notFoundTitle: 'This page is not available',
    notFoundDescription:
      'The link may be outdated, or the page may have been moved. Return to a valid screen to continue.',
    serverError: 'Server error',
  },
  auth: {
    login: 'Login',
    logout: 'Logout',
    register: 'Register',
    username: 'Username',
    password: 'Password',
    email: 'Email',
    name: 'Name',
    namePlaceholder: 'Enter your full name',
    loginTitle: 'Sign in',
    loginHint: 'Use your assigned username and password to continue.',
    registerTitle: 'Create an account',
    loginButton: 'Sign in',
    registerButton: 'Create account',
    keepLogin: 'Keep me signed in',
    keepLoginHint: 'Restore your session automatically the next time you open the app.',
    usernameRequired: 'Username is required.',
    passwordRequired: 'Password is required.',
    invalidCredentials: 'Your username or password is incorrect.',
    loginFailed: 'Unable to sign in right now. Please try again.',
    logoutConfirmMessage: 'Do you want to sign out from this session?',
    logoutFailed:
      'Unable to notify the server before signing out. Your local session has been cleared.',
    usernamePlaceholder: 'Enter your username',
    passwordPlaceholder: 'Enter your password',
    emailNotConfirmed: 'Please verify your email address before signing in.',
    emailRequired: 'Email is required.',
    emailPlaceholder: 'Enter your email address',
    resendVerification: 'Resend verification email',
    resendVerificationSuccess:
      'If an account exists for this email, a verification link has been sent.',
    resendVerificationFailed:
      'Unable to resend the verification email right now. Please try again.',
    nameRequired: 'Name is required.',
    emailInvalid: 'Enter a valid email address.',
    passwordTooShort: 'Password must be at least 8 characters.',
    usernameAlreadyExists: 'This username is already taken.',
    emailAlreadyExists: 'An account with this email already exists.',
    registerFailed: 'Unable to create your account right now. Please try again.',
    registerSuccessTitle: 'Check your email',
    registerSuccessMessage:
      "We've sent a verification link to {email}. Verify your email to sign in.",
    backToLogin: 'Already have an account? Sign in',
    verifyingEmail: 'Verifying your email…',
    verifyEmailInvalidToken: 'This verification link is invalid or has expired.',
    verifyEmailMissingToken: 'This verification link is missing or malformed.',
    verifyEmailFailed: 'Unable to verify your email right now. Please try again.',
    orContinueWith: 'Or continue with',
    continueWithGoogle: 'Sign in with Google',
    googleEmailNotConfirmed:
      'An account with this email already exists but has not been verified yet. Verify it below to link Google sign-in.',
    googleEmailNotVerifiedByProvider:
      "Google hasn't verified this email address. Please verify it with Google first.",
    googleLoginFailed: 'Unable to sign in with Google right now. Please try again.',
    continueWithMicrosoft: 'Sign in with Microsoft',
    microsoftEmailNotConfirmed:
      'An account with this email already exists but has not been verified yet. Verify it below to link Microsoft sign-in.',
    microsoftEmailNotVerifiedByProvider:
      "Microsoft hasn't verified this email address. Please verify it with Microsoft first.",
    microsoftLoginFailed: 'Unable to sign in with Microsoft right now. Please try again.',
    registerLink: "Don't have an account? Create one",
  },
  app: {
    name: 'App Starter',
    version: 'Version {version}',
    copyright: '© 2026 App Starter',
    apiVersion: 'API v{version}',
    apiBuildTime: 'Build {time}',
    apiStatus: {
      checking: 'Checking API…',
      online: 'API online',
      offline: 'API offline',
    },
    welcomeTitle: 'Welcome back',
    welcomeDescription: 'This is your starting point — build your first feature from here.',
  },
  nav: {
    home: 'Home',
    organizations: 'Organizations',
    profile: 'Profile',
    toggleSidebar: 'Toggle sidebar',
    collapseSidebar: 'Collapse sidebar',
    expandSidebar: 'Expand sidebar',
  },
  profile: {
    title: 'Profile',
    updateSuccess: 'Profile updated',
    passwordChanged: 'Password changed',
    currentPassword: 'Current password',
    newPassword: 'New password',
    changePassword: 'Change password',
    personalInfo: 'Personal information',
    currentPasswordPlaceholder: 'Enter your current password',
    newPasswordPlaceholder: 'Enter a new password',
    name: 'Name',
    namePlaceholder: 'Enter the full name',
    email: 'Email',
    emailPlaceholder: 'Enter the email address',
    phone: 'Phone',
    phonePlaceholder: 'Enter the phone number',
    position: 'Position',
    positionPlaceholder: 'Enter the position',
    address: 'Address',
    addressPlaceholder: 'Enter the address',
    sessionsTitle: 'Active sessions',
    sessionsEmpty: 'No active sessions.',
    currentDevice: 'This device',
    rememberLogin: 'Remember login',
    lastActive: 'Last active',
    sessionLoginAt: 'Signed in',
    unknownDevice: 'Unknown device',
    unknownIp: 'Unknown IP',
    signOutDevice: 'Sign out',
    signOutOtherDevices: 'Sign out of other devices',
    signOutDeviceConfirm: 'Sign out of this device?',
    signOutOtherDevicesConfirm: 'This will sign you out of every other device. Continue?',
    deviceSignedOut: 'Device signed out',
    otherDevicesSignedOut: 'Signed out of other devices',
  },
  organizations: {
    title: 'Organizations',
    personalWorkspace: 'Personal workspace',
    switchOrganization: 'Switch organization',
    createNew: 'New organization',
    createTitle: 'Create organization',
    manage: 'Manage organizations',
    name: 'Name',
    namePlaceholder: 'Enter the organization name',
    slug: 'Slug',
    slugPlaceholder: 'e.g. acme-inc',
    slugHint: 'A unique, URL-safe identifier. Lowercase letters, numbers, and hyphens only.',
    myRole: 'Your role',
    status: 'Status',
    active: 'Active',
    inactive: 'Inactive',
    empty: 'No organizations yet',
    emptyHint: 'Create an organization to get started.',
    created: 'Organization created',
    switchedTo: 'Switched to {name}',
    switchedToPersonal: 'Switched to your personal workspace',
    members: 'Members',
    membersTitle: 'Members of {name}',
    membersEmpty: 'No members yet.',
    addMember: 'Add member',
    addMemberTitle: 'Add member',
    memberEmail: 'Email',
    memberEmailPlaceholder: "Enter the member's email address",
    memberAdded: 'Member added',
    removeMember: 'Remove member',
    removeMemberConfirm: 'Remove this member from the organization?',
    memberRemoved: 'Member removed',
    memberRolesUpdated: 'Roles updated',
    deactivate: 'Deactivate organization',
    deactivateConfirm:
      'Deactivate this organization? All members will immediately lose access. This cannot be undone.',
    deactivated: 'Organization deactivated',
    roles: 'Roles',
    rolesTitle: 'Roles of {name}',
    rolesEmpty: 'No roles yet.',
    roleNoPermissions: 'No permissions',
    createRole: 'Create role',
    createRoleTitle: 'Create role',
    editRoleTitle: 'Edit role',
    roleName: 'Name',
    roleNamePlaceholder: 'Enter the role name',
    rolePermissions: 'Permissions',
    roleDeleteConfirm:
      'Delete this role? Members holding only this role will lose its permissions.',
    roleCreated: 'Role created',
    roleUpdated: 'Role updated',
    roleDeleted: 'Role deleted',
    roleSystemBadge: 'System',
    permissionOrganizationManage: 'Manage organization',
    permissionOrganizationMembersManage: 'Manage members',
    permissionOrganizationRolesManage: 'Manage roles',
  },
}

export default en
