---
layout: default
title: Privacy Policy
permalink: /privacy/
---

# Privacy Policy — Hosts File Editor

**Effective date:** July 1, 2026

Hosts File Editor ("the app") is a Windows desktop application for viewing and editing your computer's local `hosts` file. This policy explains how the app handles your information. It applies to both editions of the app — **Hosts File Editor (classic)** and **Hosts File Editor (modern)**.

## The short version

Hosts File Editor does **not** collect, transmit, store, or share any personal data. There are no user accounts, no analytics, no telemetry, no advertising, and no tracking. Everything the app does happens locally on your own device.

## Information we collect

**None.** The developer does not receive any information from the app. The app has no server component and sends no data to the developer or to any third party.

## Data stored on your device

The app reads and writes files only on your own computer:

- Your system **`hosts` file** (`%WinDir%\System32\drivers\etc\hosts`), which you are editing.
- A **backup** of your hosts file, plus any named **archives**, application **settings**, and window state, stored in your per-user application data folder (`%LocalAppData%\HostsFileEditor`).

This data never leaves your device and is never sent anywhere.

## Network activity

The app makes network connections only for the **optional availability check ("ping")** feature. When you enable auto-ping or manually check an entry, the app sends a standard ping (ICMP echo) directly to the IP address you entered in your own hosts file, solely to show you whether that address responds. These requests go only to the addresses you specified — never to the developer — and no results are recorded or transmitted anywhere. If you do not use this feature, the app makes no network connections.

The app may also flush your computer's local DNS resolver cache after you change the hosts file. This is a local operation on your machine.

## Permissions

Editing the system hosts file requires administrator rights, so the app requests elevation on demand (a Windows UAC prompt) only when you save changes or enable/disable the hosts file. It runs as a standard user otherwise.

## Children's privacy

The app is not directed at children and collects no data from anyone.

## Changes to this policy

Any updates will be posted on this page with a revised effective date.

## Contact

Questions about this policy? Contact Scott Lerch at [scottlerch@gmail.com](mailto:scottlerch@gmail.com).

---

[← Back to Hosts File Editor](https://hostsfileeditor.com/)
