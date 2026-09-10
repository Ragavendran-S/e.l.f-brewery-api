
jobs:
  build:
    runs-on: ubuntu-latest
    env:
      JWT_KEY: ${{ secrets.JWT_KEY }}
      JWT_ISSUER: ${{ secrets.JWT_ISSUER }}
