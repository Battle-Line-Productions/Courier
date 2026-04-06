import { Hero } from "@/components/home/hero";
import { Features } from "@/components/home/features";
import { QuickStart } from "@/components/home/quick-start";
import { ScreenshotShowcase } from "@/components/home/screenshot-showcase";
import { TechStack } from "@/components/home/tech-stack";
import {
  JsonLd,
  softwareApplicationLd,
  websiteLd,
} from "@/components/seo/json-ld";

export default function HomePage() {
  return (
    <>
      <JsonLd data={softwareApplicationLd} />
      <JsonLd data={websiteLd} />
      <Hero />
      <Features />
      <ScreenshotShowcase />
      <QuickStart />
      <TechStack />
    </>
  );
}
