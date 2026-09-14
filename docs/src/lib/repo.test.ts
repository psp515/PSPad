import { describe, expect, it } from 'vitest'
import { join, parseEnvExample, readRepoFile } from './repo'

describe('parseEnvExample', () => {
  it('takes the description from the comment directly above the assignment', () => {
    const entries = parseEnvExample('# Database name.\nMONGO_DATABASE=pspad\n')

    expect(entries).toEqual([{ name: 'MONGO_DATABASE', value: 'pspad', description: 'Database name.' }])
  })

  it('produces no entry for blank lines or stand-alone comments', () => {
    const entries = parseEnvExample('# A heading comment\n\n# Database name.\nMONGO_DATABASE=pspad\n')

    expect(entries).toHaveLength(1)
    expect(entries[0].description).toBe('Database name.')
  })

  it('keeps a value containing an equals sign intact', () => {
    const entries = parseEnvExample('# Realm URL.\nKEYCLOAK_AUTHORITY=http://localhost:8080/realms/pspad?a=b\n')

    expect(entries[0].value).toBe('http://localhost:8080/realms/pspad?a=b')
  })

  it('gives an entry with no comment above it an empty description', () => {
    const entries = parseEnvExample('MONGO_DATABASE=pspad\n')

    expect(entries[0].description).toBe('')
  })

  it('does not carry a description past the entry that used it', () => {
    const entries = parseEnvExample('# Database name.\nMONGO_DATABASE=pspad\nMONGO_PASSWORD=secret\n')

    expect(entries[1].description).toBe('')
  })
})

describe('readRepoFile', () => {
  it('resolves relative to the repository root', () => {
    expect(readRepoFile('docker/.env.example')).toContain('MONGO_PASSWORD=')
  })
})

describe('join', () => {
  it('joins a base ending in a slash without doubling it', () => {
    expect(join('/PSPad/', 'install')).toBe('/PSPad/install')
  })

  it('joins a base with no trailing slash', () => {
    expect(join('/PSPad', 'install')).toBe('/PSPad/install')
  })

  it('returns the base itself for an empty path', () => {
    expect(join('/PSPad/', '')).toBe('/PSPad/')
  })
})
