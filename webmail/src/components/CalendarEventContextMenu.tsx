import React, { useEffect, useRef } from 'react';
import type { CalendarEvent } from '../types';

interface CalendarEventContextMenuProps {
  x: number;
  y: number;
  event: CalendarEvent;
  canEdit: boolean;
  isOrganizer: boolean;
  onOpen: (event: CalendarEvent) => void;
  onEdit: (event: CalendarEvent) => void;
  onResend: (event: CalendarEvent) => void;
  onDelete: (event: CalendarEvent) => void;
  onClose: () => void;
}

const MENU_WIDTH = 220;
const MENU_HEIGHT = 150;

// Position a right-click menu so it never opens off-screen.
const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

export const CalendarEventContextMenu: React.FC<CalendarEventContextMenuProps> = ({
  x,
  y,
  event,
  canEdit,
  isOrganizer,
  onOpen,
  onEdit,
  onResend,
  onDelete,
  onClose,
}) => {
  const menuRef = useRef<HTMLDivElement | null>(null);

  // Dismiss on Escape, any outside click, another right-click, scroll, or resize —
  // the same contract the inbox menu honours.
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    const onScroll = (e: Event) => {
      const el = menuRef.current;
      const target = e.target;
      if (el && target instanceof Node && el.contains(target)) return;
      onClose();
    };

    document.addEventListener('click', onClose);
    document.addEventListener('contextmenu', onClose);
    document.addEventListener('keydown', onKeyDown);
    window.addEventListener('scroll', onScroll, true);
    window.addEventListener('resize', onClose);

    return () => {
      document.removeEventListener('click', onClose);
      document.removeEventListener('contextmenu', onClose);
      document.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('scroll', onScroll, true);
      window.removeEventListener('resize', onClose);
    };
  }, [onClose]);

  const style: React.CSSProperties = {
    top: clamp(y, 8, Math.max(8, window.innerHeight - MENU_HEIGHT - 8)),
    left: clamp(x, 8, Math.max(8, window.innerWidth - MENU_WIDTH - 8)),
  };

  return (
    <div className="wm-context-menu" ref={menuRef} role="menu" style={style}>
      <div className="wm-context-item" role="menuitem" onClick={() => onOpen(event)}>
        Open
      </div>
      {canEdit && (
        <>
          {isOrganizer && (
            <>
              <div className="wm-context-item" role="menuitem" onClick={() => onEdit(event)}>
                Edit &amp; send update
              </div>
              <div className="wm-context-item" role="menuitem" onClick={() => onResend(event)}>
                Resend invitations
              </div>
            </>
          )}
          <div className="wm-context-separator" />
          <div className="wm-context-item danger" role="menuitem" onClick={() => onDelete(event)}>
            Delete
          </div>
        </>
      )}
    </div>
  );
};
