import { useEffect, useState } from 'react';
import { emailApi } from '../api/emailApi';
import type { EmailStatusDto } from '../types/email';

/** The support line (address, the user's own address, whether it can be used). Null until loaded or on error —
 * the line simply isn't shown then. */
export function useEmailStatus() {
  const [status, setStatus] = useState<EmailStatusDto | null>(null);

  useEffect(() => {
    let cancelled = false;
    emailApi
      .status()
      .then((s) => {
        if (!cancelled) setStatus(s);
      })
      .catch(() => {
        if (!cancelled) setStatus(null);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return status;
}
