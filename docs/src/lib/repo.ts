import { readFileSync } from 'node:fs'
import path from 'node:path'

const repositoryRoot = path.resolve(process.cwd(), '..')

export interface EnvEntry {
  name: string
  value: string
  description: string
}

export function readRepoFile(relativePath: string): string {
  return readFileSync(path.join(repositoryRoot, relativePath), 'utf8')
}

export function parseEnvExample(contents: string): EnvEntry[] {
  const entries: EnvEntry[] = []
  let description = ''

  for (const line of contents.split(/\r?\n/)) {
    const trimmed = line.trim()

    if (trimmed.startsWith('#')) {
      description = trimmed.slice(1).trim()
      continue
    }

    const separator = trimmed.indexOf('=')

    if (trimmed !== '' && separator > 0) {
      entries.push({
        name: trimmed.slice(0, separator),
        value: trimmed.slice(separator + 1),
        description
      })
    }

    description = ''
  }

  return entries
}

export function join(base: string, relativePath: string): string {
  return `${base}/${relativePath}`.replace(/\/{2,}/g, '/')
}
