# OpenUsage Product Context

<!-- impeccable:product-schema 1 -->

## Platform

Native desktop: macOS (SwiftUI/AppKit) and Windows (C#/.NET, WPF presentation hosted by WinForms).
This design task concerns Windows. The macOS application's code and release process are separate.

## Users

The owner uses Windows and wants to check the limits of their AI coding subscriptions in a small
tray application. They confirmed the Windows functionality works and asked for an interface like
the existing Mac edition, with more pleasant and configurable visualization.

## Product Purpose

Show provider-reported usage limits, balances and reset times across accounts added by the user.
Preserve working sign-in, multiple accounts, saved ordering and protected credentials.

## Capabilities And Constraints

Ten provider integrations already exist. Accounts are added by explicit browser/device sign-in or
API-key entry. Never discover another tool's credentials or estimate spend from logs or prices.
Metric order and initial favorites inherit the Swift descriptors. Presentation settings change
how those same metrics are displayed, not the provider data or its meaning.

## Brand Commitments

The user explicitly chose the Mac edition as the visual reference. Preserve the original OpenUsage
and provider marks, quiet hierarchy, grouped surfaces and thin blue usage bars. The old Windows
utility-style interface is the anti-reference. Use Windows-native input and keyboard affordances.

## Evidence On Hand

- `assets/screenshot.jpg`: the committed Mac reference also visible in the user's attachment.
- `Sources/OpenUsage/Support/Theme.swift`: current Mac spacing, grouped surfaces and severity colors.
- Current Windows provider clients, OAuth and regression fixtures.
- UI review captures use clearly synthetic account labels and provider responses; they are not real
  usage claims. The old screenshot's spend chart depends on features absent from the current model.

## Open Decisions

The user explicitly delegated choosing the most useful additional visualization settings. This implementation offers theme,
density, cards/list, used/remaining, reset format, favorites, account-label privacy and text size;
their values are saved locally. Existing account files retain the prior used-value convention.
