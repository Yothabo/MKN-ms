// generate-icons.js
import sharp from 'sharp'
import fs from 'fs'

const sizes = [48, 72, 96, 144, 192, 512]

async function generateIcons() {
  if (!fs.existsSync('public/icons')) {
    fs.mkdirSync('public/icons', { recursive: true })
  }

  for (const size of sizes) {
    await sharp('src/assets/MKN.png')
      .resize(size, size)
      .toFile(`public/icons/icon-${size}.png`)
    console.log(`✅ Generated icon-${size}.png`)
  }
}

generateIcons().catch(console.error)
