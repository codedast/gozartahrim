# gozartahrim | گذر تحریم

### فورکی از v2rayN با قابلیت‌های اضافه، مخصوص عبور از فیلترینگ
### A fork of [v2rayN](https://github.com/2dust/v2rayN) with extra features, built to bypass internet censorship

[![Release](https://img.shields.io/github/v/release/codedast/gozartahrim?logo=github&label=Release)](https://github.com/codedast/gozartahrim/releases)
[![Downloads](https://img.shields.io/github/downloads/codedast/gozartahrim/total?logo=github&label=Downloads)](https://github.com/codedast/gozartahrim/releases)
[![Telegram Channel](https://img.shields.io/badge/Telegram-gozartahrim-26A5E4?logo=telegram)](https://t.me/gozartahrim)

[![Windows](https://img.shields.io/badge/Windows-supported-0078D6?logo=windows)](https://github.com/codedast/gozartahrim)

---

## دانلود / Download

آخرین نسخه رو از این‌جا دانلود کن:

Download the latest release here:

**[https://github.com/codedast/gozartahrim/releases](https://github.com/codedast/gozartahrim/releases)**

هر ریلیز دو تا فایل zip داره (خودکفا، بدون نیاز به نصب چیز دیگه‌ای) — فقط باز کن و `gozartahrim.exe` رو اجرا کن:
- `gozartahrim-*-win-x64-avalonia.zip` — رابط کاربری Avalonia (پیشنهادی)
- `gozartahrim-*-win-x64-wpf.zip` — رابط کاربری WPF

Each release ships two self-contained zip files — just extract and run `gozartahrim.exe`, no separate install needed:
- `gozartahrim-*-win-x64-avalonia.zip` — Avalonia UI (recommended)
- `gozartahrim-*-win-x64-wpf.zip` — WPF UI

---

## قابلیت‌های اضافه‌شده نسبت به v2rayN اصلی / Added on top of upstream v2rayN

### فارسی

- **برندسازی مجدد:** نام و آیکون برنامه به gozartahrim تغییر کرده؛ آیکون هنگام فعال بودن System Proxy قرمز می‌شه.
- **هسته‌ی Xray از قبل داخل برنامه:** نیازی به دانلود دستی هسته بعد از نصب نیست.
- **Alt IP Finder:** پیدا کردن IP جایگزین برای سرورهای VLESS پشت Cloudflare، از دو منبع (رنج‌های رسمی Cloudflare و جستجوی FOFA)، با پرست کشورهای پرکاربرد (ایران، آمریکا، ترکیه، آلمان، انگلیس، چین ستاره‌دار)، تست خودکار IP ها و ذخیره‌ی نتایج معتبر در یه گروه ساب‌اسکریپشن جدید.
- **اتصال خودکار به بهترین سرور:** به‌صورت جداگانه برای هر گروه ساب‌اسکریپشن قابل فعال‌سازیه؛ به‌طور دوره‌ای سرورهای گروه رو تست (پینگ + سرعت) می‌کنه و در صورت پیدا شدن سروری بهتر از سرور فعلی، خودکار بهش سوییچ می‌کنه.
- **اطلاع‌رسانی از طریق کانال تلگرام:** یه پاپ‌آپ معرفی کانال هنگام اجرای برنامه، و نمایش نوتیفیکیشن واقعی ویندوزی برای پست‌های جدید کانال (بدون نیاز به بات یا توکن).

### English

- **Rebranding:** app name and icon changed to gozartahrim; the icon turns red while System Proxy is active.
- **Bundled Xray-core:** no manual core download required after installing.
- **Alt IP Finder:** finds alternate front-IPs for VLESS-over-Cloudflare profiles from two sources (Cloudflare's official IPv4 ranges and FOFA search), with priority country presets (Iran, US, Turkey, Germany, UK, China starred), automatic testing, and saving of valid results into a new subscription group.
- **Auto-connect to the best server:** opt-in per subscription group; periodically speed-tests the group's servers and automatically switches to a better one if found.
- **Telegram channel notifications:** a promo popup on startup, plus native Windows notifications for new channel posts (no bot or token required).

---

## آموزش استفاده از قابلیت‌ها (فارسی)

### ۱. Alt IP Finder (پیدا کردن IP جایگزین)

وقتی یه سرور VLESS پشت Cloudflare داری و IP فعلیش کند شده یا فیلتر شده، می‌تونی به‌جاش چند تا IP جایگزین پیدا کنی:

1. توی لیست سرورها، روی سرور VLESS مورد نظرت راست‌کلیک کن.
2. گزینه‌ی **Find alternate IPs...** رو بزن.
3. از دراپ‌داون **Preset**، یه کشور انتخاب کن (کشورهای ستاره‌دار مثل ایران، آمریکا، ترکیه، آلمان، انگلیس و چین در اولویت‌ان). در صورت نیاز می‌تونی خودت هم Query رو دستی ویرایش کنی.
4. مقدار **Sample count per source** و **Test concurrency** رو در صورت نیاز تغییر بده (پیش‌فرض‌ها معمولاً کافی‌ان).
5. دکمه‌ی **Start search** رو بزن و صبر کن تا IP ها گرفته و تست بشن.
6. بعد از پایان، یه گروه ساب‌اسکریپشن جدید با اسم `{نام سرور} - Alt IPs` ساخته می‌شه و برنامه خودکار روی همون گروه سوییچ می‌کنه؛ اسم هر IP هم مشخص می‌کنه از Cloudflare اومده (`CL`) یا FOFA (`F`).

### ۲. اتصال خودکار به بهترین سرور

اگه یه گروه ساب‌اسکریپشن با چند تا سرور داری (مثلاً همون گروهی که از Alt IP Finder ساخته شده) و می‌خوای برنامه خودش همیشه به بهترین‌شون وصل بمونه:

1. از لیست ساب‌اسکریپشن‌ها، روی گروه مورد نظر **Edit** بزن.
2. پایین فرم، سوییچ **«اتصال خودکار به بهترین سرور این گروه»** رو روشن کن و Save کن.
3. از این به بعد، برنامه هر چند دقیقه یک‌بار (قابل تنظیم توی Settings) سرورهای اون گروه رو تست پینگ و سرعت می‌کنه و اگه سروری بهتر از سرور فعلی پیدا شد، خودکار بهش وصل می‌شه.

> نکته: این قابلیت فقط برای گروه‌هایی که خودت فعالش کردی کار می‌کنه، نه همه‌ی ساب‌اسکریپشن‌ها.

برای تنظیم فاصله‌ی هر چک و تعداد سروری که هر بار تست می‌شه:

Settings → پایین تب اول → «فاصله‌ی هر چک (دقیقه)» و «تعداد سرور تست‌شده در هر چک»

### ۳. نوتیفیکیشن پیام‌های کانال تلگرام

برنامه به‌صورت خودکار هر چند دقیقه (و همچنین بلافاصله بعد از هر اتصال موفق) آخرین پست‌های کانال [gozartahrim@](https://t.me/gozartahrim) رو چک می‌کنه و در صورت وجود پست جدید، یه نوتیفیکیشن ویندوزی نشون می‌ده. برای دیدن پست کامل روی نوتیفیکیشن کلیک کن.

### ۴. تشخیص وضعیت System Proxy از روی آیکون

آیکون برنامه (هم توی پنجره‌ی اصلی هم توی Tray) وقتی System Proxy فعال/اجباری باشه، قرمز رنگ می‌شه — یعنی سریع می‌تونی بفهمی ترافیک سیستم داره از طریق پروکسی رد می‌شه یا نه، بدون نیاز به باز کردن پنجره‌ی اصلی.

---

## مستندات پروژه‌ی اصلی / Upstream documentation

برای راهنمای استفاده و تنظیمات عمومی (که در این فورک هم صدق می‌کنه)، ویکی پروژه‌ی اصلی رو ببین:

For general usage and configuration guides (which still apply to this fork), see the upstream wiki:

[https://github.com/2dust/v2rayN/wiki](https://github.com/2dust/v2rayN/wiki)

---

## پلتفرم‌های پشتیبانی‌شده / Supported Platforms

| Platform | x64 |
| --- | --- |
| Windows | ✅ |

> این فورک روی ویندوز تست و ریلیز می‌شه. پلتفرم‌های دیگه (لینوکس/مک) از سورس قابل build هستن ولی باینری آماده‌ی جداگانه‌ای منتشر نمی‌شه.
>
> This fork is tested and released for Windows. Other platforms (Linux/macOS) can be built from source but no separate prebuilt binaries are published here.

---

## کامیونیتی / Community

کانال تلگرام / Telegram Channel:

[https://t.me/gozartahrim](https://t.me/gozartahrim)

پشتیبانی / Support:

[https://t.me/mehrzero](https://t.me/mehrzero)

---

## پروژه‌ی اصلی / Original project

این پروژه فورکی از [2dust/v2rayN](https://github.com/2dust/v2rayN) هست. برای گزارش باگ‌های مربوط به هسته‌ی اصلی برنامه (نه قابلیت‌های اضافه‌شده‌ی این فورک)، به ریپوی اصلی مراجعه کن.

This project is a fork of [2dust/v2rayN](https://github.com/2dust/v2rayN). For issues in the core app itself (not this fork's added features), please refer to the original repository.
