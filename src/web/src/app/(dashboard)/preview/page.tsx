import PreviewChat from "@/components/PreviewChat";

export default function PreviewPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold">Test your agent</h1>
        <p className="text-sm text-gray-500">
          A live preview using your current knowledge, tone, and handoff rules.
          Nothing here is saved.
        </p>
      </div>
      <PreviewChat />
    </div>
  );
}
