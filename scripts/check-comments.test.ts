import { describe, expect, test } from 'bun:test'
import { findComments, isChecked } from './check-comments'

const lines = (n: number, prefix = '// ') =>
  Array.from({ length: n }, (_, i) => `${prefix}line ${i}`).join('\n')

describe('findComments', () => {
  test('allows a block of exactly three lines', () => {
    expect(findComments(lines(3)).long).toEqual([])
  })

  test('flags a fourth line with the block start and length', () => {
    expect(findComments(`const a = 1\n${lines(4)}\nconst b = 2`).long).toEqual([
      { line: 2, length: 4 },
    ])
  })

  test('a blank line or code line splits blocks', () => {
    expect(findComments(`${lines(3)}\n\n${lines(3)}`).long).toEqual([])
    expect(findComments(`${lines(3)}\nfoo()\n${lines(3)}`).long).toEqual([])
  })

  test('a trailing comment after code is not a comment line', () => {
    expect(findComments(`${lines(3)}\nfoo() // why`).long).toEqual([])
  })

  test('counts every line of a /* */ block, including CSS lines without a leading *', () => {
    const css = '/* one\n   two\n   three\n   four */\n.a { color: red; }'
    expect(findComments(css).long).toEqual([{ line: 1, length: 4 }])
  })

  test('JSDoc delimiter-only lines do not count', () => {
    expect(findComments('/**\n * one\n * two\n * three\n */\nexport const a = 1').long).toEqual([])
  })

  test('XML doc tag-only lines do not count, tagged prose does', () => {
    const doc = '/// <summary>\n/// One.\n/// </summary>\n/// <returns>Two.</returns>'
    expect(findComments(doc).long).toEqual([])
    expect(
      findComments(`${doc}\n/// <remarks>Three.</remarks>\n/// <remarks>Four.</remarks>`).long,
    ).toEqual([{ line: 2, length: 4 }])
  })

  test('per-parameter and per-response XML doc lines do not count', () => {
    const params = Array.from(
      { length: 6 },
      (_, i) => `/// <param name="p${i}">Field ${i}.</param>`,
    )
    const doc = [
      '/// <summary>One.</summary>',
      ...params,
      '/// <response code="200">Ok.</response>',
    ]
    expect(findComments(doc.join('\n')).long).toEqual([])
  })

  test('HTML comments in a Vue template count like any other', () => {
    expect(findComments('<!--\n  a\n  b\n  c\n  d\n-->').long).toEqual([{ line: 2, length: 4 }])
  })

  test('directives are not counted', () => {
    const src = `${lines(3)}\n// eslint-disable-next-line no-console\nconsole.log(1)`
    expect(findComments(src).long).toEqual([])
    expect(findComments(`// @ts-expect-error wrong type\n${lines(3)}`).long).toEqual([])
  })

  test('flags a counted comment line wider than 120 chars', () => {
    const wide = `// ${'x'.repeat(118)}`
    expect(findComments(wide).wide).toEqual([{ line: 1, length: 121 }])
    expect(findComments(`// ${'x'.repeat(117)}`).wide).toEqual([])
  })

  test('a one-line XML doc element is exempt from the width cap but still counts', () => {
    const remarks = `/// <remarks>${'x'.repeat(130)}</remarks>`
    expect(findComments(remarks).wide).toEqual([])
    expect(findComments(`/// ${'x'.repeat(130)}`).wide).toEqual([{ line: 1, length: 134 }])
    expect(findComments(Array(4).fill(remarks).join('\n')).long).toEqual([{ line: 1, length: 4 }])
  })

  test('per-parameter XML doc lines are exempt from the width cap', () => {
    const param = `/// <param name="p">${'x'.repeat(130)}</param>`
    expect(findComments(param).wide).toEqual([])
  })

  test('reports change-history wording as a hint', () => {
    const { history } = findComments('// Originally #3d3071, now lighter.\nconst c = 1')
    expect(history).toEqual([{ line: 1, length: 1, history: 'Originally' }])
  })
})

describe('isChecked', () => {
  test('covers hand-written source files', () => {
    expect(isChecked('frontend/src/main.ts')).toBe(true)
    expect(isChecked('frontend/src/App.vue')).toBe(true)
    expect(isChecked('backend/src/StarterKit.API/Program.cs')).toBe(true)
    expect(isChecked('frontend/src/assets/styles/tailwind.css')).toBe(true)
    expect(isChecked('backend/src/X/Migrations/20261016090000_NormalizeAccountIdentity.cs')).toBe(
      true,
    )
  })

  test('skips generated code and non-source files', () => {
    expect(isChecked('backend/src/X/Migrations/20261016090000_Init.Designer.cs')).toBe(false)
    expect(isChecked('backend/src/X/Migrations/AppDbContextModelSnapshot.cs')).toBe(false)
    expect(isChecked('frontend/src/api/generated/client.ts')).toBe(false)
    expect(isChecked('frontend/src/typings/components.d.ts')).toBe(false)
    expect(isChecked('README.md')).toBe(false)
    expect(isChecked('lefthook.yml')).toBe(false)
  })
})
