# macOS Releases


Releases are automated: pushing a `v*` tag on `main` builds, signs, notarizes, and publishes a new
version. A plain tag (`v0.8.0`) ships to everyone; a pre-release suffix (`v0.8.0-beta.1`) ships to
the beta channel. The pipeline lives in
[.github/workflows/release.yml](../.github/workflows/release.yml), and the step-by-step is in the
`release-swift` skill.

### Release setup (one-time)

The release workflow needs these repository secrets (Settings → Secrets and variables → Actions):

| Secret                       | What it is                                                            |
| ---------------------------- | --------------------------------------------------------------------- |
| `APPLE_CERTIFICATE`          | base64 of your Developer ID Application `.p12`                        |
| `APPLE_CERTIFICATE_PASSWORD` | the password set when exporting that `.p12`                           |
| `APPLE_ID`                   | the Apple ID email used for notarization                              |
| `APPLE_PASSWORD`             | an app-specific password for that Apple ID                            |
| `APPLE_TEAM_ID`              | your Apple Developer team ID                                          |
| `SPARKLE_PUBLIC_KEY`         | base64 EdDSA public key, baked into the build as `SUPublicEDKey`      |
| `SPARKLE_PRIVATE_KEY`        | base64 EdDSA private key used to sign the DMG                         |
| `POSTHOG_CLI_API_KEY`        | PostHog personal API key used to upload dSYMs for crash symbolication |
| `POSTHOG_CLI_PROJECT_ID`     | numeric PostHog project ID the dSYMs upload to                        |

Export the Developer ID Application cert (with its private key) from Keychain Access as a `.p12`,
then `base64 -i DeveloperID.p12 | pbcopy`. App-specific passwords come from appleid.apple.com →
Sign-In and Security → App-Specific Passwords. Generate the Sparkle EdDSA key pair once with Sparkle's
`generate_keys` tool; the public and private values must be a matching pair or signing is silently
skipped.

The two `POSTHOG_CLI_*` secrets only upload debug symbols (dSYMs) so PostHog can symbolicate crash
reports. `POSTHOG_CLI_API_KEY` is a PostHog personal API key (PostHog → Settings → Personal API keys)
and `POSTHOG_CLI_PROJECT_ID` is the numeric ID from your project URL. The upload host is hardcoded in
the workflow (`https://us.i.posthog.com`). These two do not block a release: if `POSTHOG_CLI_API_KEY`
is unset the workflow skips the upload with a warning, and crash reports for that version show raw
addresses instead of symbolicated stack traces.

The repository must be public (Sparkle fetches the DMG and appcast anonymously), and the appcast is
served from GitHub Pages. Confirm Settings → Pages points at the `gh-pages` branch after the first
release.


