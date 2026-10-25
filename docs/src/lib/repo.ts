export function join(base: string, relativePath: string): string {
  return `${base}/${relativePath}`.replace(/\/{2,}/g, '/')
}
