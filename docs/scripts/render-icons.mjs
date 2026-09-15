import { copyFile, mkdir } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
const icon = path.join(repoRoot, 'brand', 'icon.svg')
const socialCard = path.join(repoRoot, 'brand', 'og.svg')

const pngTargets = [
  { source: icon, file: 'src/PSPad.App/wwwroot/favicon.png', width: 64, height: 64 },
  { source: icon, file: 'src/PSPad.App/wwwroot/icon-192.png', width: 192, height: 192 },
  { source: icon, file: 'src/PSPad.App/wwwroot/icon-512.png', width: 512, height: 512 },
  { source: icon, file: 'docs/public/icon-512.png', width: 512, height: 512 },
  { source: socialCard, file: 'docs/public/og.png', width: 1200, height: 630 }
]

for (const target of pngTargets) {
  const destination = path.join(repoRoot, target.file)
  await mkdir(path.dirname(destination), { recursive: true })
  await sharp(target.source, { density: 384 })
    .resize(target.width, target.height)
    .png()
    .toFile(destination)
  console.log(`${target.file} ${target.width}x${target.height}`)
}

const favicon = path.join(repoRoot, 'docs/public/favicon.svg')
await copyFile(icon, favicon)
console.log('docs/public/favicon.svg')
