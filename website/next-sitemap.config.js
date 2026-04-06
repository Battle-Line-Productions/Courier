/** @type {import('next-sitemap').IConfig} */
module.exports = {
  siteUrl: "https://couriermft.com",
  generateRobotsTxt: true,
  outDir: "./out",
  robotsTxtOptions: {
    policies: [
      {
        userAgent: "*",
        allow: "/",
      },
    ],
  },
};
