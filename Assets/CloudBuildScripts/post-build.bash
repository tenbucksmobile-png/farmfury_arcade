#!/bin/bash
# Uploads the just-built IPA to App Store Connect (TestFlight) at the end of a Unity Build
# Automation iOS build. Runs on Unity's own macOS build agent, not your machine — this is what
# gets Farm Fury Arcade from "IPA built" to "sitting in TestFlight" with no Mac of your own,
# since Build Automation itself has no built-in dashboard toggle for this (confirmed against
# Unity's own support docs: https://support.unity.com/hc/en-us/articles/27576236407956).
#
# Adapted from the community-vetted reference at
# https://github.com/Dans1997/UnityAppleConnectAutomation — same structure/logic, hardened with
# `set -euo pipefail`, explicit file-existence checks, and cleanup of the decrypted key afterward
# (a plaintext private key left sitting on disk, even on an ephemeral build agent, is worth
# tidying up rather than leaving).
#
# ---- One-time setup (do this BEFORE the first Cloud Build run that expects this script) ----
#
# 1. Encrypt your real App Store Connect API key (the .p8 file Apple only lets you download
#    once) — run this LOCALLY, on your own machine. openssl ships with Git Bash on Windows, so
#    you don't need a Mac for this step either:
#
#      openssl aes-256-cbc -pbkdf2 -in AuthKey_7S8MJ6XZ4P.p8 -out authkey.p8.enc -k "CHOOSE-A-STRONG-PASSWORD-HERE"
#
#    Commit ONLY the resulting authkey.p8.enc to this folder. NEVER commit the raw .p8 file —
#    the whole point of this step is that the real private key never touches the git repo.
#
# 2. In the Unity Cloud dashboard: DevOps -> Build Automation -> Configurations -> (your iOS
#    config) -> Advanced Settings -> Environment Variables, add three variables:
#      API_KEY_ID       = 7S8MJ6XZ4P   (dedicated "Unity Cloud Build - Farm Fury Arcade" key —
#                          not a key shared with any other project)
#      API_ISSUER_ID    = 7bbe865f-2c12-4f76-9500-74d4b62c5e51   (your real Issuer ID — shared
#                          per-team across all your keys, correct to reuse as-is)
#      ENCRYPTION_KEY   = the exact same password you used with openssl in step 1 — mark it
#                          secret/masked if the dashboard offers that option, since this is the
#                          one value that actually unlocks the private key.
#
# 3. In the same iOS config's Script Hooks section, set Post-Build Script to:
#      Assets/CloudBuildScripts/post-build.bash
#
# 4. Confirm this file is committed with its executable bit set (git doesn't reliably preserve
#    that from a Windows checkout) — from a shell with git access:
#      git update-index --chmod=+x Assets/CloudBuildScripts/post-build.bash
#
# ---- What this script actually does at build time ----

set -euo pipefail

echo "=== Farm Fury Arcade: post-build TestFlight upload ==="

ipa_path="$WORKSPACE/.build/last/$TARGET_NAME/build.ipa"
encrypted_key_path="$WORKSPACE/Assets/CloudBuildScripts/authkey.p8.enc"
decrypted_key_path=~/private_keys/AuthKey_"$API_KEY_ID".p8

cleanup() {
  # Runs on both success and failure (EXIT trap) — don't leave a decrypted private key sitting
  # on the build agent's disk any longer than the upload command actually needs it for.
  if [ -f "$decrypted_key_path" ]; then
    rm -f "$decrypted_key_path"
    echo "Cleaned up decrypted key at $decrypted_key_path"
  fi
}
trap cleanup EXIT

if [ ! -f "$ipa_path" ]; then
  echo "ERROR: expected IPA not found at $ipa_path — the build itself may have failed before this script ran, or Unity's output path convention has changed since this script was written."
  exit 1
fi
echo "Found built IPA: $ipa_path"

if [ ! -f "$encrypted_key_path" ]; then
  echo "ERROR: encrypted API key not found at $encrypted_key_path — did you complete step 1 above (encrypt and commit authkey.p8.enc)?"
  exit 1
fi

if [ -z "${API_KEY_ID:-}" ] || [ -z "${API_ISSUER_ID:-}" ] || [ -z "${ENCRYPTION_KEY:-}" ]; then
  echo "ERROR: API_KEY_ID, API_ISSUER_ID, or ENCRYPTION_KEY environment variable is missing — set all three in the iOS config's Advanced Settings (step 2 above)."
  exit 1
fi

echo "Decrypting App Store Connect API key..."
mkdir -p ~/private_keys/
if ! openssl aes-256-cbc -pbkdf2 -d -in "$encrypted_key_path" -out "$decrypted_key_path" -k "$ENCRYPTION_KEY"; then
  echo "ERROR: decryption failed — ENCRYPTION_KEY doesn't match the password used to encrypt authkey.p8.enc, or the .enc file is corrupt."
  exit 1
fi
echo "Decrypted to $decrypted_key_path"

echo "Uploading to App Store Connect (TestFlight)..."
if xcrun altool --upload-app -t ios -f "$ipa_path" \
    --apiKey "$API_KEY_ID" \
    --apiIssuer "$API_ISSUER_ID"; then
  echo "Upload succeeded — check App Store Connect -> TestFlight -> Builds; it will show Processing for a while before it's ready to add testers to."
else
  echo "ERROR: xcrun altool upload failed — see the output above for Apple's specific rejection reason (a common one on a first attempt: Export Compliance hasn't been answered yet for this app)."
  exit 1
fi
